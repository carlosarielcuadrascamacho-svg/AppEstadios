// Traemos los cerebros de la aplicación que manejan la lógica de los eventos
using AppEstadios.Controllers;
// Traemos los moldes de datos básicos para representar equipos y partidos
using AppEstadios.Models;

namespace AppEstadios.Views
{
    // Esta es la parte lógica de la pantalla donde registramos los partidos de fútbol.
    // Su trabajo principal es manejar lo que el usuario ve en su teléfono: llena las listas
    // desplegables con los equipos y estadios disponibles, captura los textos que se escriben,
    // y le avisa al usuario si el registro del juego fue exitoso o si algo salió mal.
    public partial class RegistroEventoView : ContentPage
    {
        // Guardamos la referencia del controlador que sabe cómo procesar la información
        private readonly RegistroEventoController _controlador;

        // Creamos una lista vacía para guardar a los clubes que participan
        private List<Equipo> _equipos = new();

        // Creamos una lista vacía para guardar los recintos deportivos
        private List<Estadio> _estadios = new();

        // Este apartado inicializa todos los botones y textos de la pantalla en cuanto
        // el usuario la abre por primera vez. También prepara el controlador que procesará
        // los datos y configura efectos visuales interactivos para que todo se sienta fluido.
        public RegistroEventoView()
        {
            // Ensambla los controles visuales definidos en el diseño XAML
            InitializeComponent();

            // Preparamos el controlador para usarlo a lo largo de la pantalla
            _controlador = new RegistroEventoController();

            // Ejecutamos la configuración de las animaciones de los botones
            ConfigurarAnimacionesInteractivas();
        }

        // Esta función añade pequeños efectos visuales como encoger un poco los botones
        // al presionarlos o agrandar ligeramente las cajas de texto al escribir en ellas.
        // Esto ayuda a que la aplicación se sienta mucho más profesional al usarla.
        private void ConfigurarAnimacionesInteractivas()
        {
            // Hacemos que el botón de guardar se encoja tantito cuando el dedo lo presiona
            btnGuardar.Pressed += async (s, e) => await btnGuardar.ScaleTo(0.95, 100, Easing.SinOut);
            // Hacemos que recupere su tamaño original al levantar el dedo de la pantalla
            btnGuardar.Released += async (s, e) => await btnGuardar.ScaleTo(1.0, 100, Easing.SinOut);

            // Juntamos todas las cajitas de texto y listas desplegables en un solo grupo
            var inputs = new View[] { pickerLocal, pickerVisitante, pickerEstadio, fechaEvento, horaEvento, txtTotalBoletos, txtPrecio };
            
            // Revisamos cada control del grupo uno por uno
            foreach (var input in inputs)
            {
                // Si el usuario selecciona la cajita para escribir en ella
                input.Focused += async (s, e) => 
                {
                    if (s is View v)
                    {
                        // Buscamos si la cajita tiene un borde decorativo alrededor
                        var border = ObtenerBorderPadre(v);
                        // Si tiene borde, agrandamos el borde ligeramente
                        if (border != null)
                            await border.ScaleTo(1.02, 150, Easing.CubicOut);
                        // Si no tiene, agrandamos la cajita completa
                        else
                            await v.ScaleTo(1.02, 150, Easing.CubicOut);
                    }
                };
                
                // Cuando el usuario deja de escribir y quita la selección de la cajita
                input.Unfocused += async (s, e) => 
                {
                    if (s is View v)
                    {
                        var border = ObtenerBorderPadre(v);
                        // Regresamos el borde a su tamaño normal y original
                        if (border != null)
                            await border.ScaleTo(1.0, 150, Easing.CubicOut);
                        // Regresamos el control completo a su escala normal
                        else
                            await v.ScaleTo(1.0, 150, Easing.CubicOut);
                    }
                };
            }
        }

        // Es una pequeña utilidad que busca el contorno o borde que rodea a una caja de texto
        // para aplicarle los efectos visuales de selección sin afectar al resto del diseño general.
        private Border ObtenerBorderPadre(Element elemento)
        {
            // Conseguimos el elemento visual que contiene al control actual
            var parent = elemento.Parent;
            // Subimos de nivel en el diseño hasta encontrar la capa más exterior
            while (parent != null)
            {
                // Si encontramos un borde decorativo, lo devolvemos de inmediato
                if (parent is Border border)
                    return border;
                // Seguimos buscando hacia arriba en el árbol visual
                parent = parent.Parent;
            }
            // Si no había ningún borde contenedor, devolvemos un valor nulo
            return null;
        }

        // Estas funciones se ejecutan justo en el momento en que la pantalla se muestra ante los
        // ojos del usuario. Hacen que los bloques de texto aparezcan suavemente y cargan las listas.
        protected override async void OnAppearing()
        {
            // Respetamos el comportamiento original que tiene el teléfono al abrir pantallas
            base.OnAppearing();

            // Volvemos invisibles los pedazos del formulario antes de que empiece la animación
            HeaderSection.Opacity = 0;
            MatchupSection.Opacity = 0;
            // Los desplazamos un poquito hacia abajo para que vayan subiendo suavemente
            MatchupSection.TranslationY = 30;
            DetallesSection.Opacity = 0;
            DetallesSection.TranslationY = 30;
            BoletosSection.Opacity = 0;
            BoletosSection.TranslationY = 30;
            btnGuardar.Opacity = 0;
            btnGuardar.TranslationY = 30;

            // Iniciamos el desvanecimiento elegante de entrada
            AnimarEntradaAsync();

            // Mandamos a pedir los datos de los servidores mientras la pantalla se acomoda
            await CargarDatosInicialesAsync();

            fechaEvento.Date = DateTime.Today.AddDays(7); // Una semana adelante como punto de partida
            horaEvento.Time  = new TimeSpan(20, 0, 0);   // 20:00 hrs, horario más común para partidos
        }

        // Esta función coordina el efecto escalonado de aparición de los elementos visuales en pantalla,
        // logrando una transición suave y agradable al iniciar la navegación.
        private async void AnimarEntradaAsync()
        {
            // Hacemos aparecer el título principal de la ventana
            _ = HeaderSection.FadeTo(1, 400, Easing.CubicOut);
            
            // Esperamos una décima de segundo antes de mostrar el siguiente bloque
            await Task.Delay(100);
            // Subimos y mostramos el bloque de elección de equipos contendientes
            _ = Task.WhenAll(
                MatchupSection.FadeTo(1, 400, Easing.CubicOut),
                MatchupSection.TranslateTo(0, 0, 400, Easing.CubicOut)
            );

            await Task.Delay(100);
            // Hacemos aparecer el espacio de selección de fechas y estadios
            _ = Task.WhenAll(
                DetallesSection.FadeTo(1, 400, Easing.CubicOut),
                DetallesSection.TranslateTo(0, 0, 400, Easing.CubicOut)
            );

            await Task.Delay(100);
            // Revelamos el apartado del costo de los boletos
            _ = Task.WhenAll(
                BoletosSection.FadeTo(1, 400, Easing.CubicOut),
                BoletosSection.TranslateTo(0, 0, 400, Easing.CubicOut)
            );

            await Task.Delay(100);
            // Por último, hacemos visible el gran botón de confirmación final
            _ = Task.WhenAll(
                btnGuardar.FadeTo(1, 400, Easing.CubicOut),
                btnGuardar.TranslateTo(0, 0, 400, Easing.CubicOut)
            );
        }

        // Esta función le pide al sistema que traiga a los equipos y los recintos deportivos para
        // ponerlos dentro de las opciones. Añade también un botón especial para registrar sedes nuevas.
        private async Task CargarDatosInicialesAsync()
        {
            // Solicitamos traer los catálogos al mismo tiempo para ganar velocidad
            var tareaEquipos  = _controlador.ObtenerEquiposAsync();
            var tareaEstadios = _controlador.ObtenerEstadiosAsync();
            await Task.WhenAll(tareaEquipos, tareaEstadios);

            // Guardamos la información recolectada en nuestras variables internas
            _equipos  = tareaEquipos.Result;
            _estadios = tareaEstadios.Result;

            // Agregamos un botón interactivo falso al final de la lista para crear estadios
            _estadios.Add(new Estadio { Id = -1, Nombre = "➕ Agregar nuevo estadio..." });

            // Limpiamos las opciones viejas que pudieran haber quedado grabadas
            pickerLocal.ItemsSource      = null;
            pickerVisitante.ItemsSource  = null;
            pickerEstadio.ItemsSource    = null;

            // Llenamos los controles visuales con las listas actualizadas de datos
            pickerLocal.ItemsSource      = _equipos;
            pickerVisitante.ItemsSource  = _equipos;
            pickerEstadio.ItemsSource    = _estadios;

            // Si no encontramos equipos en internet, avisamos de inmediato al usuario
            if (_equipos.Count == 0)
                await DisplayAlert("Sin datos", "No se pudieron cargar los equipos. Verifica tu conexión.", "OK");

            // Si solo quedó la opción falsa de agregar estadio, avisamos que faltan datos
            if (_estadios.Count == 1) 
                await DisplayAlert("Sin datos", "No se pudieron cargar los estadios. Verifica tu conexión.", "OK");

            // Bloqueamos al segundo equipo hasta que el usuario decida quién juega primero
            pickerVisitante.IsEnabled = false;
            pickerVisitante.Opacity   = 0.45;
        }

        // Sirve para actualizar de manera invisible la lista de recintos disponibles y selecciona
        // automáticamente el último estadio que hayamos dado de alta para facilitarle el trabajo.
        private async Task RecargarEstadiosAsync()
        {
            // Traemos nuevamente las sedes desde internet por si hay cambios
            var estadiosActualizados = await _controlador.ObtenerEstadiosAsync();
            _estadios = estadiosActualizados;
            // Volvemos a colar la opción secreta de añadir uno nuevo
            _estadios.Add(new Estadio { Id = -1, Nombre = "➕ Agregar nuevo estadio..." });

            // Refrescamos la interfaz visual para que dibuje las nuevas opciones
            pickerEstadio.ItemsSource = null;
            pickerEstadio.ItemsSource = _estadios;

            // Dejamos marcado de forma automática el penúltimo estadio de la lista
            if (_estadios.Count > 1)
            {
                pickerEstadio.SelectedIndex = _estadios.Count - 2;
            }
        }

        // Se activa cuando el usuario escoge una opción en la lista de lugares. Si la persona
        // presiona la opción de "Agregar nuevo", abre una ventana emergente de creación.
        private async void OnEstadioSeleccionado(object sender, EventArgs e)
        {
            // Evaluamos si el usuario tocó justamente el botón falso de creación
            if (pickerEstadio.SelectedItem is Estadio seleccionado && seleccionado.Id == -1)
            {
                // Quitamos la selección para que no se quede guardado el botón falso
                pickerEstadio.SelectedIndex = -1;

                // Preparamos la ventana flotante donde se dibuja el mapa de creación
                var modal = new AgregarEstadioView();
                // Le enseñamos qué debe hacer cuando esa ventanita se cierre en el futuro
                modal.Disappearing += async (s, args) =>
                {
                    // Al regresar a esta pantalla, volvemos a traer los estadios creados
                    await RecargarEstadiosAsync();
                };
                
                // Levantamos la ventana emergente sobre la vista actual
                await Navigation.PushModalAsync(modal);
            }
        }

        // Se encarga de evitar que un club de fútbol juegue contra sí mismo. Al elegir el equipo de casa,
        // esta función automáticamente filtra y limpia la lista del equipo visitante.
        private void OnEquipoLocalSeleccionado(object? sender, EventArgs e)
        {
            // Olvidamos la selección anterior del visitante para no provocar errores
            pickerVisitante.SelectedIndex = -1;

            // Si el usuario desmarcó la opción del equipo local
            if (pickerLocal.SelectedItem is not Equipo localElegido)
            {
                // Volvemos a apagar la segunda lista por seguridad
                pickerVisitante.IsEnabled   = false;
                pickerVisitante.Opacity     = 0.45;
                pickerVisitante.ItemsSource = _equipos;
                return;
            }

            // Filtramos para quedarnos solo con los equipos que NO sean el local
            var equiposFiltrados = _equipos
                .Where(eq => eq.Id != localElegido.Id)
                .ToList();

            // Entregamos la lista podada a la segunda selección
            pickerVisitante.ItemsSource = equiposFiltrados;
            // Le devolvemos la vida al control para que el usuario pueda interactuar
            pickerVisitante.IsEnabled   = true;
            pickerVisitante.Opacity     = 1.0;
            pickerVisitante.Title       = "Seleccionar";
        }

        // Es la acción principal que se ejecuta al pulsar el botón de registrar. Recoge todas
        // las selecciones de fechas, precios y nombres para mandarlas a confirmar mediante alertas.
        private async void OnRegistrarPartidoClicked(object sender, EventArgs e)
        {
            // Leemos qué objetos completos eligió la persona en los menús desplegables
            var equipoLocal     = pickerLocal.SelectedItem as Equipo;
            var equipoVisitante = pickerVisitante.SelectedItem as Equipo;
            var estadio         = pickerEstadio.SelectedItem as Estadio;

            // Extraemos las cadenas de texto que se anotaron para las cantidades
            var totalBoletosTexto = txtTotalBoletos.Text?.Trim() ?? string.Empty;
            var precioTexto       = txtPrecio.Text?.Trim() ?? string.Empty;

            // Obtenemos el día calendario y el momento horario del partido
            var fecha = fechaEvento.Date ?? DateTime.Today;
            var hora  = horaEvento.Time  ?? TimeSpan.Zero;

            // Convertimos el remitente para tener acceso al botón físico
            var boton = (Button)sender;
            // Congelamos el botón para que nadie le dé múltiples clics por desesperación
            boton.IsEnabled = false;

            // Enviamos el paquete gigante de datos al controlador para su verificación formal
            var resultado = await _controlador.GuardarEventoAsync(
                equipoLocal,
                equipoVisitante,
                estadio,
                totalBoletosTexto,
                precioTexto,
                fecha,
                hora);

            // Si la base de datos nos responde que todo se guardó exitosamente
            if (resultado.Exitoso)
            {
                // Mostramos un mensaje festivo al usuario final en su pantalla
                await DisplayAlert("¡Partido Registrado! 🎉", resultado.Mensaje, "Aceptar");
                // Vaciamos todos los campos para poder dar de alta otro juego después
                LimpiarFormulario(); 
            }
            // En caso de que falte información o haya problemas de programación
            else
            {
                // Mostramos una advertencia explicando el conflicto
                await DisplayAlert("⚠️ Atención", resultado.Mensaje, "Entendido");
            }

            // Volvemos a prender el botón para que pueda usarse de nuevo sin problemas
            boton.IsEnabled = true;
        }

        // Devuelve todas las casillas de texto y listas desplegables a su estado original
        // una vez que un partido ha sido registrado correctamente en el servidor.
        private void LimpiarFormulario()
        {
            // Limpiamos la selección visual de los tres menús desplegables
            pickerLocal.SelectedIndex     = -1;
            pickerVisitante.SelectedIndex = -1;
            pickerEstadio.SelectedIndex   = -1;
            // Vaciamos el contenido escrito en los cuadros de texto numéricos
            txtTotalBoletos.Text          = string.Empty;
            txtPrecio.Text                = string.Empty;
            // Reiniciamos la fecha a 7 días en adelante (más natural para nuevos eventos)
            fechaEvento.Date = DateTime.Today.AddDays(7);
            // Reiniciamos la hora a las 8 PM, horario estándar de partidos nocturnos
            horaEvento.Time  = new TimeSpan(20, 0, 0);

            // Dejamos inhabilitado el picker visitante como estaba en el principio
            pickerVisitante.ItemsSource = _equipos;
            pickerVisitante.IsEnabled   = false;
            pickerVisitante.Opacity     = 0.45;
            pickerVisitante.Title       = "Primero elige local";
        }
    }
}