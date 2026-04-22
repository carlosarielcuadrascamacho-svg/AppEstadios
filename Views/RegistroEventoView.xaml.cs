using AppEstadios.Controllers;
using AppEstadios.Models;

namespace AppEstadios.Views
{
    /// <summary>
    /// RegistroEventoView — Code-behind de la pantalla de registro de partidos.
    ///
    /// ROL EN MVC (View):
    ///   Este archivo es exclusivamente responsable de:
    ///     1. Inicializar el controlador.
    ///     2. Cargar los datos de los Pickers al abrir la pantalla.
    ///     3. Leer los valores de la UI y pasarlos al controlador.
    ///     4. Mostrar los resultados (alertas, estado de carga) al usuario.
    ///
    ///   NO contiene lógica de negocio ni consultas a la base de datos.
    ///   Todo eso es responsabilidad de RegistroEventoController.
    /// </summary>
    public partial class RegistroEventoView : ContentPage
    {
        // ──────────────────────────────────────────────────────────
        //  REFERENCIA AL CONTROLADOR
        // ──────────────────────────────────────────────────────────

        /// <summary>
        /// Instancia del controlador que maneja toda la lógica y datos.
        /// Se crea una vez y se reutiliza durante el ciclo de vida de la pantalla.
        /// </summary>
        private readonly RegistroEventoController _controlador;

        // ──────────────────────────────────────────────────────────
        //  LISTAS DE DATOS PARA LOS PICKERS
        // ──────────────────────────────────────────────────────────

        /// <summary>
        /// Lista de equipos cargados desde Supabase.
        /// Se usa como ItemsSource de pickerLocal y pickerVisitante.
        /// </summary>
        private List<Equipo> _equipos = new();

        /// <summary>
        /// Lista de estadios cargados desde Supabase.
        /// Se usa como ItemsSource de pickerEstadio.
        /// </summary>
        private List<Estadio> _estadios = new();

        // ──────────────────────────────────────────────────────────
        //  CONSTRUCTOR
        // ──────────────────────────────────────────────────────────

        public RegistroEventoView()
        {
            InitializeComponent();

            // Instanciamos el controlador (sin parámetros por ahora)
            _controlador = new RegistroEventoController();
        }

        // ──────────────────────────────────────────────────────────
        //  CICLO DE VIDA: OnAppearing
        // ──────────────────────────────────────────────────────────

        /// <summary>
        /// Se ejecuta cada vez que esta pantalla aparece en pantalla.
        /// Es el lugar correcto para cargar los datos iniciales de forma asíncrona.
        /// </summary>
        protected override async void OnAppearing()
        {
            base.OnAppearing();
            await CargarDatosInicialesAsync();
        }

        /// <summary>
        /// Carga las listas de equipos y estadios desde Supabase en paralelo
        /// para llenar los Pickers de la UI.
        ///
        /// Usamos Task.WhenAll() para hacer ambas solicitudes simultáneamente,
        /// lo que reduce el tiempo de espera del usuario a la mitad.
        /// </summary>
        private async Task CargarDatosInicialesAsync()
        {
            // Mostramos indicador de actividad mientras cargamos
            // (puedes agregar un ActivityIndicator en el XAML y manejarlo aquí)

            // Ejecutamos ambas consultas en paralelo
            var tareaEquipos  = _controlador.ObtenerEquiposAsync();
            var tareaEstadios = _controlador.ObtenerEstadiosAsync();
            await Task.WhenAll(tareaEquipos, tareaEstadios);

            // Guardamos los resultados en las listas de la View
            _equipos  = tareaEquipos.Result;
            _estadios = tareaEstadios.Result;

            // ── Asignamos la fuente de datos a los Pickers ──
            // ItemsSource + ItemDisplayBinding permite que el Picker
            // muestre el nombre del objeto (vía ToString()) pero guardemos el objeto completo

            pickerLocal.ItemsSource      = _equipos;
            pickerVisitante.ItemsSource  = _equipos;
            pickerEstadio.ItemsSource    = _estadios;

            // Si no hay datos, alertamos al usuario
            if (_equipos.Count == 0)
                await DisplayAlert("Sin datos", "No se pudieron cargar los equipos. Verifica tu conexión.", "OK");

            if (_estadios.Count == 0)
                await DisplayAlert("Sin datos", "No se pudieron cargar los estadios. Verifica tu conexión.", "OK");
        }

        // ──────────────────────────────────────────────────────────
        //  EVENTO: BOTÓN REGISTRAR PARTIDO
        // ──────────────────────────────────────────────────────────

        /// <summary>
        /// Handler del botón "⚽ REGISTRAR PARTIDO".
        /// Lee los valores de la UI, los pasa al controlador y muestra el resultado.
        ///
        /// IMPORTANTE: Este método solo lee y muestra datos.
        ///   Toda la lógica real vive en RegistroEventoController.GuardarEventoAsync().
        /// </summary>
        private async void OnRegistrarPartidoClicked(object sender, EventArgs e)
        {
            // ── Paso 1: Leer los objetos seleccionados en los Pickers ──
            // El cast es seguro porque el ItemsSource son listas tipadas
            var equipoLocal     = pickerLocal.SelectedItem as Equipo;
            var equipoVisitante = pickerVisitante.SelectedItem as Equipo;
            var estadio         = pickerEstadio.SelectedItem as Estadio;

            // ── Paso 2: Leer los valores de texto de los Entry ──
            var totalBoletosTexto = txtTotalBoletos.Text?.Trim() ?? string.Empty;
            var precioTexto       = txtPrecio.Text?.Trim() ?? string.Empty;

            // ── Paso 3: Leer Fecha y Hora de los pickers ──
            // En .NET 10 estos son nullable, usamos ?? para un valor por defecto seguro
            var fecha = fechaEvento.Date ?? DateTime.Today;
            var hora  = horaEvento.Time  ?? TimeSpan.Zero;

            // ── Paso 4: Delegar al controlador ──
            // El botón queda deshabilitado durante el proceso para evitar doble tap
            var boton = (Button)sender;
            boton.IsEnabled = false;

            var resultado = await _controlador.GuardarEventoAsync(
                equipoLocal,
                equipoVisitante,
                estadio,
                totalBoletosTexto,
                precioTexto,
                fecha,
                hora);

            // ── Paso 5: Mostrar resultado al usuario ──
            if (resultado.Exitoso)
            {
                await DisplayAlert("¡Partido Registrado! 🎉", resultado.Mensaje, "Aceptar");
                LimpiarFormulario(); // Resetear la UI para un nuevo registro
            }
            else
            {
                await DisplayAlert("⚠️ Atención", resultado.Mensaje, "Entendido");
            }

            // Rehabilitar el botón
            boton.IsEnabled = true;
        }

        // ──────────────────────────────────────────────────────────
        //  MÉTODO AUXILIAR: Limpiar formulario
        // ──────────────────────────────────────────────────────────

        /// <summary>
        /// Resetea todos los campos del formulario a su estado inicial
        /// después de un registro exitoso.
        /// </summary>
        private void LimpiarFormulario()
        {
            pickerLocal.SelectedIndex     = -1;
            pickerVisitante.SelectedIndex = -1;
            pickerEstadio.SelectedIndex   = -1;
            txtTotalBoletos.Text          = string.Empty;
            txtPrecio.Text                = string.Empty;
            fechaEvento.Date = DateTime.Today;
            horaEvento.Time  = TimeSpan.Zero;
        }
    }
}