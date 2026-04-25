using AppEstadios.Controllers;
using AppEstadios.Services;
using Microsoft.Maui.Controls.Maps;
using Microsoft.Maui.Maps;

namespace AppEstadios.Views
{
    public partial class ReportesView : ContentPage
    {
        private readonly ReportesController _controlador;
        private readonly Dictionary<Pin, PinEventoInfo> _pinDataMap = new();

        // ── ESTADOS MODO RUTA ──
        private bool _modoRutaActivado = false;
        private Pin? _pinOrigenTemp;
        private Polyline? _rutaPolyline;
        private PinEventoInfo? _eventoRutaSeleccionado;

        public ReportesView()
        {
            InitializeComponent();
            _controlador = new ReportesController();
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();

            // Preparación inicial de opacidades y traslaciones para animar
            headerOverlay.Opacity = 0;
            headerOverlay.TranslationY = -20;
            headerOverlay.IsVisible = false;

            overlayVacio.Opacity = 0;
            overlayVacio.TranslationY = 20;
            overlayVacio.IsVisible = false;

            fondoOscuro.Opacity = 0;
            fondoOscuro.IsVisible = false;

            bottomSheet.Opacity = 0;
            bottomSheet.TranslationY = 150;
            bottomSheet.IsVisible = false;

            overlayLoading.Opacity = 0;
            overlayLoading.Scale = 0.95;

            await InicializarMapaAsync();
        }

        // ──────────────────────────────────────────────────────────
        //  INICIALIZACIÓN Y FLUJO PRINCIPAL
        // ──────────────────────────────────────────────────────────

        private async Task InicializarMapaAsync()
        {
            await MostrarLoadingAsync();

            // ── El controlador ejecuta: Filtrar → Agrupar → Seleccionar → Proyectar ──
            var pines = await _controlador.ObtenerPinesEventosFuturosAsync();

            await OcultarLoadingAsync();

            if (pines.Count == 0)
            {
                lblContadorPines.Text = "Sin eventos próximos";
                await MostrarOverlayVacioAsync();
                _ = MostrarHeaderAsync();
                return;
            }

            // ── Centrar y hacer zoom para abarcar todos los pines ──
            var region = ReportesController.CalcularRegion(pines);
            
            double radioKm = 2000;
            if (region.ZoomLevel >= 10) radioKm = 50;
            else if (region.ZoomLevel >= 7) radioKm = 300;
            
            var location = new Location(region.Latitud, region.Longitud);
            var span = MapSpan.FromCenterAndRadius(location, Distance.FromKilometers(radioKm));
            mapaEventos.MoveToRegion(span);

            var plural = pines.Count > 1 ? "s" : "";
            lblContadorPines.Text = $"{pines.Count} estadio{plural} con próximo evento";

            _ = MostrarHeaderAsync();

            // ── Crear y registrar marcadores con ligero delay ──
            await ColocarMarcadoresEnMapaAnimadosAsync(pines);
        }

        // ──────────────────────────────────────────────────────────
        //  MARCADORES
        // ──────────────────────────────────────────────────────────

        private async Task ColocarMarcadoresEnMapaAnimadosAsync(List<PinEventoInfo> pines)
        {
            mapaEventos.Pins.Clear();
            _pinDataMap.Clear();

            foreach (var pinInfo in pines)
            {
                var marcador = new Pin
                {
                    Label = pinInfo.NombreEstadio,
                    Address = "Toca para ver detalles",
                    Type = PinType.Place,
                    Location = new Location(pinInfo.Latitud, pinInfo.Longitud)
                };

                marcador.MarkerClicked += OnMarkerClicked;
                
                _pinDataMap[marcador] = pinInfo;
                mapaEventos.Pins.Add(marcador);

                // Delay para dar el efecto visual de aparición progresiva de pines
                await Task.Delay(100); 
            }
        }

        // ──────────────────────────────────────────────────────────
        //  EVENTO DE TAP EN MARCADOR
        // ──────────────────────────────────────────────────────────

        private async void OnMarkerClicked(object? sender, PinClickedEventArgs e)
        {
            e.HideInfoWindow = true; // Prevenir la ventana nativa
            
            if (sender is Pin pin && _pinDataMap.TryGetValue(pin, out var info))
            {
                if (_modoRutaActivado) return; // Evitar click en estadio si estamos eligiendo origen

                _eventoRutaSeleccionado = info;

                // Centrar mapa con animación
                var span = MapSpan.FromCenterAndRadius(pin.Location, Distance.FromKilometers(2));
                mapaEventos.MoveToRegion(span);

                // Limpiar labels mientras carga
                lblCardBoletos.Text = "...";
                lblCardRecaudado.Text = "...";

                _ = MostrarBottomSheetAsync(info);

                // Fetch Lazy Load Statistics
                var stats = await _controlador.ObtenerEstadisticasAsync(info.EventoId);
                lblCardBoletos.Text = $"{stats.BoletosVendidos} / {stats.TotalBoletos}";
                lblCardRecaudado.Text = $"${stats.TotalRecaudado:N2}";
            }
        }

        // ──────────────────────────────────────────────────────────
        //  ANIMACIONES: OVERLAYS Y COMPONENTES
        // ──────────────────────────────────────────────────────────

        private async Task MostrarLoadingAsync(string texto = "Cargando eventos...")
        {
            lblLoadingTexto.Text = texto;
            overlayLoading.IsVisible = true;
            await Task.WhenAll(
                overlayLoading.FadeTo(1, 250, Easing.SinOut),
                overlayLoading.ScaleTo(1, 250, Easing.SinOut)
            );
        }

        private async Task OcultarLoadingAsync()
        {
            await Task.WhenAll(
                overlayLoading.FadeTo(0, 300, Easing.SinIn),
                overlayLoading.ScaleTo(0.95, 300, Easing.SinIn)
            );
            overlayLoading.IsVisible = false;
        }

        private async Task MostrarHeaderAsync()
        {
            headerOverlay.IsVisible = true;
            await Task.WhenAll(
                headerOverlay.FadeTo(1, 350, Easing.CubicOut),
                headerOverlay.TranslateTo(0, 0, 350, Easing.CubicOut)
            );
        }

        private async Task MostrarOverlayVacioAsync()
        {
            overlayVacio.IsVisible = true;
            await Task.WhenAll(
                overlayVacio.FadeTo(1, 350, Easing.CubicOut),
                overlayVacio.TranslateTo(0, 0, 350, Easing.CubicOut)
            );
        }

        // ──────────────────────────────────────────────────────────
        //  ANIMACIONES: BOTTOM SHEET
        // ──────────────────────────────────────────────────────────

        private async Task MostrarBottomSheetAsync(PinEventoInfo info)
        {
            lblCardEstadio.Text = info.NombreEstadio;
            lblCardPartido.Text = $"{info.NombreLocal} vs {info.NombreVisitante}";
            lblCardFecha.Text   = info.FechaFormateada;
            lblCardHora.Text    = info.HoraFormateada;

            bottomSheet.IsVisible = true;
            fondoOscuro.IsVisible = true;

            Microsoft.Maui.Controls.ViewExtensions.CancelAnimations(bottomSheet);
            Microsoft.Maui.Controls.ViewExtensions.CancelAnimations(fondoOscuro);

            // Aparecer fondo oscuro (Fade in leve)
            _ = fondoOscuro.FadeTo(1, 300, Easing.CubicOut);

            // Bottom sheet sube y aparece
            await Task.WhenAll(
                bottomSheet.FadeTo(1, 300, Easing.CubicOut),
                bottomSheet.TranslateTo(0, 0, 300, Easing.CubicOut)
            );
        }

        private async Task OcultarBottomSheetAsync()
        {
            Microsoft.Maui.Controls.ViewExtensions.CancelAnimations(bottomSheet);
            Microsoft.Maui.Controls.ViewExtensions.CancelAnimations(fondoOscuro);

            _ = fondoOscuro.FadeTo(0, 250, Easing.CubicIn);

            await Task.WhenAll(
                bottomSheet.FadeTo(0, 250, Easing.CubicIn),
                bottomSheet.TranslateTo(0, 150, 250, Easing.CubicIn)
            );

            bottomSheet.IsVisible = false;
            fondoOscuro.IsVisible = false;
        }

        private async void OnMapaTapped(object? sender, Microsoft.Maui.Controls.TappedEventArgs e)
        {
            if (bottomSheet.IsVisible)
                await OcultarBottomSheetAsync();
        }

        // ──────────────────────────────────────────────────────────
        //  MODO RUTA: EVENTOS Y LÓGICA
        // ──────────────────────────────────────────────────────────

        private void OnMapClicked(object? sender, MapClickedEventArgs e)
        {
            if (!_modoRutaActivado) return;

            // Remover el origen anterior si existe
            if (_pinOrigenTemp != null)
            {
                mapaEventos.Pins.Remove(_pinOrigenTemp);
            }

            _pinOrigenTemp = new Pin
            {
                Label = "Mi Origen",
                Type = PinType.Generic,
                Location = e.Location
            };
            
            mapaEventos.Pins.Add(_pinOrigenTemp);
            btnConfirmarRuta.IsEnabled = true;
        }

        private async void OnComoLlegarClicked(object? sender, EventArgs e)
        {
            await OcultarBottomSheetAsync();
            
            _modoRutaActivado = true;
            headerOverlay.IsVisible = false;
            toolbarRuta.IsVisible = true;
            btnConfirmarRuta.IsEnabled = false;

            // Limpiar ruta anterior si se va a crear una nueva
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

        private async void OnCancelarRutaClicked(object? sender, EventArgs e)
        {
            _modoRutaActivado = false;
            toolbarRuta.IsVisible = false;
            
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
            
            await MostrarHeaderAsync();
        }

        private async void OnConfirmarRutaClicked(object? sender, EventArgs e)
        {
            if (_pinOrigenTemp == null || _eventoRutaSeleccionado == null) return;

            // Desactivar modo ruta y ocultar toolbar
            _modoRutaActivado = false;
            toolbarRuta.IsVisible = false;
            btnConfirmarRuta.IsEnabled = false;

            // Limpiar polyline anterior
            if (_rutaPolyline != null)
            {
                mapaEventos.MapElements.Remove(_rutaPolyline);
                _rutaPolyline = null;
            }

            // Mostrar loading mientras consultamos la API
            await MostrarLoadingAsync("Calculando ruta...");

            var locOrigen  = _pinOrigenTemp.Location;
            var locDestino = new Location(_eventoRutaSeleccionado.Latitud, _eventoRutaSeleccionado.Longitud);

            // ── Llamar a Google Directions API ──
            var resultado = await GoogleDirectionsService.ObtenerRutaAsync(locOrigen, locDestino);

            await OcultarLoadingAsync();

            // ── Manejar resultado ──
            if (!resultado.Exito)
            {
                // Construir mensaje con el estado exacto de Google para facilitar el diagnóstico
                var detalle = string.IsNullOrWhiteSpace(resultado.MensajeError)
                    ? resultado.Estado
                    : $"{resultado.Estado}: {resultado.MensajeError}";

                bool abrirNativo = await DisplayAlert(
                    $"Ruta no disponible [{resultado.Estado}]",
                    resultado.Estado == "REQUEST_DENIED"
                        ? $"La clave API está bloqueando llamadas REST.\n\n" +
                          $"Detalle: {detalle}\n\n" +
                          $"¿Abrimos Google Maps para ver la ruta?"
                        : $"No se pudo calcular la ruta.\n\nDetalle: {detalle}\n\n" +
                          $"¿Abrimos Google Maps para ver la ruta?",
                    "Abrir Google Maps",
                    "Cancelar");

                if (abrirNativo)
                    await GoogleDirectionsService.AbrirEnGoogleMapsNativoAsync(locOrigen, locDestino);

                await MostrarHeaderAsync();
                return;
            }

            // ── Construir Polyline con los puntos decodificados ──
            _rutaPolyline = new Polyline
            {
                StrokeColor = Color.FromArgb("#1565C0"),  // Azul Google Maps
                StrokeWidth = 6
            };

            foreach (var punto in resultado.Puntos)
                _rutaPolyline.Geopath.Add(punto);

            mapaEventos.MapElements.Add(_rutaPolyline);

            // Encuadrar el mapa para mostrar la ruta completa
            var centerLat = (locOrigen.Latitude  + locDestino.Latitude)  / 2;
            var centerLon = (locOrigen.Longitude + locDestino.Longitude) / 2;
            var center    = new Location(centerLat, centerLon);

            var distance     = Location.CalculateDistance(locOrigen, locDestino, DistanceUnits.Kilometers);
            var radioEncuadre = Math.Max(distance * 0.75, 1);

            mapaEventos.MoveToRegion(MapSpan.FromCenterAndRadius(center, Distance.FromKilometers(radioEncuadre)));

            await MostrarHeaderAsync();
        }
    }
}