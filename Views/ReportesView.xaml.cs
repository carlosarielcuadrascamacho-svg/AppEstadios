// Traemos los controladores encargados de las estadísticas y reportes
using AppEstadios.Controllers;
// Traemos las herramientas para calcular los caminos y rutas
using AppEstadios.Services;
// Traemos los controles para manipular marcas y trazos sobre el mapa
using Microsoft.Maui.Controls.Maps;
// Traemos las funciones de posicionamiento geográfico estándar
using Microsoft.Maui.Maps;

namespace AppEstadios.Views
{
    // Esta pantalla es el centro de mando interactivo que dibuja sobre el mapa mundial todos
    // los próximos partidos de fútbol. Permite a los usuarios tocar cualquier estadio para
    // ver cuánto dinero se ha ganado y calcular caminos en auto desde su casa.
    public partial class ReportesView : ContentPage
    {
        // Guardamos el controlador que nos da la información financiera
        private readonly ReportesController _controlador;
        
        // Guardamos un diccionario para saber qué partido le pertenece a cada marca del mapa
        private readonly Dictionary<Pin, PinEventoInfo> _pinDataMap = new();

        // Esta variable indica si el usuario está en modo de elegir su punto de salida
        private bool _modoRutaActivado = false;
        
        // Guarda temporalmente el lugar de salida que el usuario marcó
        private Pin? _pinOrigenTemp;
        
        // Guarda la línea azul del camino trazado sobre las calles
        private Polyline? _rutaPolyline;
        
        // Recuerda qué estadio seleccionamos para ir a visitarlo
        private PinEventoInfo? _eventoRutaSeleccionado;

        // Prepara el inicio visual de la pantalla del mapa e instala el controlador
        // adecuado para poder platicar directamente con la base de datos en internet.
        public ReportesView()
        {
            // Cargamos los componentes visuales estructurados en el diseño
            InitializeComponent();
            
            // Inicializamos el controlador de reportes generales
            _controlador = new ReportesController();
        }

        // Prepara el terreno visual reseteando el estado de los menús flotantes
        // y ocultando las pantallas de carga para que luego aparezcan de forma animada.
        protected override async void OnAppearing()
        {
            // Respetamos el encendido normal de la ventana en el teléfono
            base.OnAppearing();

            // Volvemos invisible el encabezado y lo subimos tantito
            headerOverlay.Opacity = 0;
            headerOverlay.TranslationY = -20;
            headerOverlay.IsVisible = false;

            // Hacemos lo mismo con el aviso de falta de datos
            overlayVacio.Opacity = 0;
            overlayVacio.TranslationY = 20;
            overlayVacio.IsVisible = false;

            // Oscurecemos el fondo levemente para resaltar las tarjetas
            fondoOscuro.Opacity = 0;
            fondoOscuro.IsVisible = false;

            // Escondemos la tarjeta de detalles abajo de la pantalla
            bottomSheet.Opacity = 0;
            bottomSheet.TranslationY = 150;
            bottomSheet.IsVisible = false;

            // Ocultamos el círculo de espera
            overlayLoading.Opacity = 0;
            overlayLoading.Scale = 0.95;

            // Mandamos a encender el mapa y traer los datos
            await InicializarMapaAsync();
        }

        // Carga todos los partidos del futuro, enciende una animación mientras los trae
        // del servidor y calcula el tamaño perfecto del mapa para que todo quepa en pantalla.
        private async Task InicializarMapaAsync()
        {
            // Mostramos el letrero de que estamos cargando los datos
            await MostrarLoadingAsync();

            // Le pedimos al controlador la lista de los estadios con juegos futuros
            var pines = await _controlador.ObtenerPinesEventosFuturosAsync();

            // Ocultamos el letrero de carga una vez completada la búsqueda
            await OcultarLoadingAsync();

            // Evaluamos si regresamos con las manos vacías de internet
            if (pines.Count == 0)
            {
                // Escribimos que no hay eventos en las etiquetas visuales
                lblContadorPines.Text = "Sin eventos próximos";
                // Enseñamos el dibujo que indica que la lista está vacía
                await MostrarOverlayVacioAsync();
                // Mostramos la barra superior
                _ = MostrarHeaderAsync();
                return;
            }

            // Averiguamos el punto medio geográfico para centrar la cámara del mapa
            var region = ReportesController.CalcularRegion(pines);
            
            // Calculamos la altura de la cámara según lo dispersos que estén los estadios
            double radioKm = 2000;
            if (region.ZoomLevel >= 10) radioKm = 50;
            else if (region.ZoomLevel >= 7) radioKm = 300;
            
            // Apuntamos la cámara del teléfono hacia las coordenadas obtenidas
            var location = new Location(region.Latitud, region.Longitud);
            var span = MapSpan.FromCenterAndRadius(location, Distance.FromKilometers(radioKm));
            mapaEventos.MoveToRegion(span);

            // Acomodamos el texto de bienvenida según cuántos estadios encontramos
            var plural = pines.Count > 1 ? "s" : "";
            lblContadorPines.Text = $"{pines.Count} estadio{plural} con próximo evento";

            // Revelamos el encabezado superior de la pantalla
            _ = MostrarHeaderAsync();

            // Colocamos los marcadores gráficos sobre el plano del mapa
            await ColocarMarcadoresEnMapaAnimadosAsync(pines);
        }

        // Dibuja pequeños pines rojos sobre los estadios en el mapa uno por uno.
        // Añade un pequeño retraso entre cada pin para crear un efecto visual progresivo.
        private async Task ColocarMarcadoresEnMapaAnimadosAsync(List<PinEventoInfo> pines)
        {
            // Borramos marcas viejas para no pintar doble
            mapaEventos.Pins.Clear();
            _pinDataMap.Clear();

            // Recorremos cada estadio encontrado para dibujarlo
            foreach (var pinInfo in pines)
            {
                // Construimos el marcador con su nombre y dirección
                var marcador = new Pin
                {
                    Label = pinInfo.NombreEstadio,
                    Address = "Toca para ver detalles",
                    Type = PinType.Place,
                    Location = new Location(pinInfo.Latitud, pinInfo.Longitud)
                };

                // Configuramos qué pasa si el usuario toca este marcador con el dedo
                marcador.MarkerClicked += OnMarkerClicked;
                
                // Guardamos la relación entre este marcador y el partido correspondiente
                _pinDataMap[marcador] = pinInfo;
                // Agregamos el marcador físico al mapa de Microsoft Maui
                mapaEventos.Pins.Add(marcador);

                // Hacemos una pequeña pausa para que aparezcan en cascada
                await Task.Delay(100); 
            }
        }

        // Se ejecuta al tocar un estadio. Abre un panel flotante desde abajo de la
        // pantalla con información del juego y calcula los ingresos económicos.
        private async void OnMarkerClicked(object? sender, PinClickedEventArgs e)
        {
            // Evitamos que aparezca la ventanita blanca nativa del mapa
            e.HideInfoWindow = true; 
            
            // Verificamos si el botón pulsado era un marcador registrado
            if (sender is Pin pin && _pinDataMap.TryGetValue(pin, out var info))
            {
                // Si estás eligiendo desde dónde sales a manejar, ignoramos este toque
                if (_modoRutaActivado) return; 

                // Recordamos que este es el estadio objetivo para la ruta
                _eventoRutaSeleccionado = info;

                // Centramos el mapa en el estadio enfocándolo muy de cerca
                var span = MapSpan.FromCenterAndRadius(pin.Location, Distance.FromKilometers(2));
                mapaEventos.MoveToRegion(span);

                // Ponemos puntos suspensivos mientras llegan los cálculos de dinero
                lblCardBoletos.Text = "...";
                lblCardRecaudado.Text = "...";

                // Levantamos la tarjeta informativa desde el suelo del teléfono
                _ = MostrarBottomSheetAsync(info);

                // Traemos las cifras exactas de ventas desde internet
                var stats = await _controlador.ObtenerEstadisticasAsync(info.EventoId);
                // Pintamos los boletos vendidos contra los totales
                lblCardBoletos.Text = $"{stats.BoletosVendidos} / {stats.TotalBoletos}";
                // Escribimos las ganancias totales acumuladas en pesos
                lblCardRecaudado.Text = $"${stats.TotalRecaudado:N2}";
            }
        }

        // Enciende un letrero de espera con el texto que le pidamos.
        private async Task MostrarLoadingAsync(string texto = "Cargando eventos...")
        {
            lblLoadingTexto.Text = texto;
            overlayLoading.IsVisible = true;
            // Hacemos que el fondo aparezca de forma sutil y crezca ligeramente
            await Task.WhenAll(
                overlayLoading.FadeTo(1, 250, Easing.SinOut),
                overlayLoading.ScaleTo(1, 250, Easing.SinOut)
            );
        }

        // Quita suavemente la animación de espera de la vista.
        private async Task OcultarLoadingAsync()
        {
            await Task.WhenAll(
                overlayLoading.FadeTo(0, 300, Easing.SinIn),
                overlayLoading.ScaleTo(0.95, 300, Easing.SinIn)
            );
            overlayLoading.IsVisible = false;
        }

        // Muestra el encabezado superior con transiciones fluidas.
        private async Task MostrarHeaderAsync()
        {
            headerOverlay.IsVisible = true;
            await Task.WhenAll(
                headerOverlay.FadeTo(1, 350, Easing.CubicOut),
                headerOverlay.TranslateTo(0, 0, 350, Easing.CubicOut)
            );
        }

        // Muestra el dibujo informativo de que no hay partidos agendados.
        private async Task MostrarOverlayVacioAsync()
        {
            overlayVacio.IsVisible = true;
            await Task.WhenAll(
                overlayVacio.FadeTo(1, 350, Easing.CubicOut),
                overlayVacio.TranslateTo(0, 0, 350, Easing.CubicOut)
            );
        }

        // Levanta la tarjeta informativa con los datos escritos de los equipos y la hora.
        private async Task MostrarBottomSheetAsync(PinEventoInfo info)
        {
            // Escribimos los nombres de los competidores en los letreros
            lblCardEstadio.Text = info.NombreEstadio;
            lblCardPartido.Text = $"{info.NombreLocal} vs {info.NombreVisitante}";
            lblCardFecha.Text   = info.FechaFormateada;
            lblCardHora.Text    = info.HoraFormateada;

            // Encendemos las capas visuales que estaban dormidas
            bottomSheet.IsVisible = true;
            fondoOscuro.IsVisible = true;

            // Frenamos animaciones a medio camino por si el usuario está jugando con los clics
            Microsoft.Maui.Controls.ViewExtensions.CancelAnimations(bottomSheet);
            Microsoft.Maui.Controls.ViewExtensions.CancelAnimations(fondoOscuro);

            // Oscurecemos el mapa poco a poco
            _ = fondoOscuro.FadeTo(1, 300, Easing.CubicOut);

            // Levantamos la tarjeta desde abajo como un cajón
            await Task.WhenAll(
                bottomSheet.FadeTo(1, 300, Easing.CubicOut),
                bottomSheet.TranslateTo(0, 0, 300, Easing.CubicOut)
            );
        }

        // Vuelve a ocultar el cajón informativo guardándolo en el fondo de la pantalla.
        private async Task OcultarBottomSheetAsync()
        {
            Microsoft.Maui.Controls.ViewExtensions.CancelAnimations(bottomSheet);
            Microsoft.Maui.Controls.ViewExtensions.CancelAnimations(fondoOscuro);

            // Aclaramos el mapa nuevamente
            _ = fondoOscuro.FadeTo(0, 250, Easing.CubicIn);

            // Bajamos el cajón hacia la parte invisible del celular
            await Task.WhenAll(
                bottomSheet.FadeTo(0, 250, Easing.CubicIn),
                bottomSheet.TranslateTo(0, 150, 250, Easing.CubicIn)
            );

            // Apagamos las vistas para liberar memoria gráfica
            bottomSheet.IsVisible = false;
            fondoOscuro.IsVisible = false;
        }

        // Se activa si el usuario toca el fondo oscuro o el mapa libre para cerrar las ventanas.
        private async void OnMapaTapped(object? sender, Microsoft.Maui.Controls.TappedEventArgs e)
        {
            if (bottomSheet.IsVisible)
                await OcultarBottomSheetAsync();
        }

        // Registra el lugar que tocaste en el mapa como tu punto de partida para viajar.
        private void OnMapClicked(object? sender, MapClickedEventArgs e)
        {
            // Si no estamos en la función de buscar rutas, ignoramos los toques
            if (!_modoRutaActivado) return;

            // Quitamos la marca de salida vieja si es que ya existía una
            if (_pinOrigenTemp != null)
            {
                mapaEventos.Pins.Remove(_pinOrigenTemp);
            }

            // Creamos un marcador genérico que dice "Mi Origen"
            _pinOrigenTemp = new Pin
            {
                Label = "Mi Origen",
                Type = PinType.Generic,
                Location = e.Location
            };
            
            // Lo soltamos en el plano visual del mapa
            mapaEventos.Pins.Add(_pinOrigenTemp);
            // Permitimos que el usuario presione el botón de confirmación final
            btnConfirmarRuta.IsEnabled = true;
        }

        // Activa el modo especial para trazar el camino hacia las gradas del estadio.
        private async void OnComoLlegarClicked(object? sender, EventArgs e)
        {
            // Guardamos el cajón de información
            await OcultarBottomSheetAsync();
            
            // Avisamos al teléfono que estamos esperando un punto de salida
            _modoRutaActivado = true;
            headerOverlay.IsVisible = false;
            toolbarRuta.IsVisible = true;
            btnConfirmarRuta.IsEnabled = false;

            // Limpiamos rastros de viajes anteriores
            if (_rutaPolyline != null)
            {
                mapaEventos.MapElements.Remove(_rutaPolyline);
                _rutaPolyline = null;
            }
            if (_pinOrigenTemp != null)
            {
                mapaEventos.Pins.Remove(_pinOrigenTemp);
                _pinOrigenTemp = null;
            }
        }

        // Cancela la acción de viaje devolviendo todo a su comportamiento habitual.
        private async void OnCancelarRutaClicked(object? sender, EventArgs e)
        {
            _modoRutaActivado = false;
            toolbarRuta.IsVisible = false;
            
            // Barremos con los elementos gráficos sobrantes
            if (_pinOrigenTemp != null)
            {
                mapaEventos.Pins.Remove(_pinOrigenTemp);
                _pinOrigenTemp = null;
            }
            
            if (_rutaPolyline != null)
            {
                mapaEventos.MapElements.Remove(_rutaPolyline);
                _rutaPolyline = null;
            }
            
            // Volvemos a mostrar la barra informativa principal
            await MostrarHeaderAsync();
        }

        // Pide formalmente el cálculo de las calles y dibuja la línea azul en el recorrido.
        private async void OnConfirmarRutaClicked(object? sender, EventArgs e)
        {
            // Si nos falta el origen o el destino, no podemos trazar nada
            if (_pinOrigenTemp == null || _eventoRutaSeleccionado == null) return;

            // Apagamos los comandos del viaje para concentrarnos en el mapa
            _modoRutaActivado = false;
            toolbarRuta.IsVisible = false;
            btnConfirmarRuta.IsEnabled = false;

            // Borramos cualquier trazo del suelo antes de empezar el nuevo
            if (_rutaPolyline != null)
            {
                mapaEventos.MapElements.Remove(_rutaPolyline);
                _rutaPolyline = null;
            }

            // Activamos el aviso flotante de espera
            await MostrarLoadingAsync("Calculando ruta...");

            // Extraemos las posiciones geográficas de salida y llegada
            var locOrigen  = _pinOrigenTemp.Location;
            var locDestino = new Location(_eventoRutaSeleccionado.Latitud, _eventoRutaSeleccionado.Longitud);

            // Pedimos al servicio de internet que nos dé el trayecto de las avenidas
            var resultado = await OsrmRoutingService.ObtenerRutaAsync(locOrigen, locDestino);

            // Apagamos el aviso de espera
            await OcultarLoadingAsync();

            // Si internet falló y no pudo calcular el viaje en coche
            if (!resultado.Exito)
            {
                var detalle = string.IsNullOrWhiteSpace(resultado.MensajeError)
                    ? resultado.Estado
                    : $"{resultado.Estado}: {resultado.MensajeError}";

                // Preguntamos si desea usar su aplicación de mapas favorita
                bool abrirNativo = await DisplayAlert(
                    $"Ruta no disponible [{resultado.Estado}]",
                    resultado.Estado == "Timeout"
                        ? $"El servidor de rutas tardó demasiado.\n\n¿Abrimos Google Maps?"
                        : $"No se pudo calcular.\n\nDetalle: {detalle}\n\n¿Abrimos Google Maps?",
                    "Abrir Google Maps",
                    "Cancelar");

                // Lanzamos la aplicación nativa en caso de que haya dicho que sí
                if (abrirNativo)
                    await OsrmRoutingService.AbrirEnMapasNativoAsync(locOrigen, locDestino);

                await MostrarHeaderAsync();
                return;
            }

            // Preparamos las brochas virtuales para pintar la línea azul
            _rutaPolyline = new Polyline
            {
                StrokeColor = Color.FromArgb("#1565C0"),  
                StrokeWidth = 6
            };

            // Agregamos cada pedazo de coordenada a la línea principal
            foreach (var punto in resultado.Puntos)
                _rutaPolyline.Geopath.Add(punto);

            // Pegamos el dibujo completo encima del mapa
            mapaEventos.MapElements.Add(_rutaPolyline);

            // Hacemos cálculos para que la cámara abarque todo el viaje de inicio a fin
            var centerLat = (locOrigen.Latitude  + locDestino.Latitude)  / 2;
            var centerLon = (locOrigen.Longitude + locDestino.Longitude) / 2;
            var center    = new Location(centerLat, centerLon);

            var distance     = Location.CalculateDistance(locOrigen, locDestino, DistanceUnits.Kilometers);
            var radioEncuadre = Math.Max(distance * 0.75, 1);

            // Desplazamos la cámara del teléfono para apreciar el camino entero
            mapaEventos.MoveToRegion(MapSpan.FromCenterAndRadius(center, Distance.FromKilometers(radioEncuadre)));

            // Devolvemos la paz visual a la cabecera
            await MostrarHeaderAsync();
        }
    }
}