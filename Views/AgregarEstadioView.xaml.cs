// Traemos el cerebro que controla las acciones relacionadas con los estadios
using AppEstadios.Controllers;
// Traemos las herramientas específicas para dibujar marcas sobre el mapa
using Microsoft.Maui.Controls.Maps;
// Traemos las funciones básicas de geolocalización y coordenadas
using Microsoft.Maui.Maps;

namespace AppEstadios.Views
{
    // Esta pantalla le permite al usuario registrar un nuevo estadio de fútbol escribiendo
    // su nombre y marcando su posición exacta sobre un mapa interactivo. Actúa como la
    // ventana visual que recoge estos datos y los envía al sistema para guardarlos.
    public partial class AgregarEstadioView : ContentPage
    {
        // Guardamos aquí el controlador que sabe cómo guardar los datos en internet
        private readonly EstadioController _controlador;
        
        // Guardamos temporalmente el punto del mapa que el usuario tocó con el dedo
        private Location? _ubicacionSeleccionada;

        // Este bloque prepara la pantalla para que esté lista para usarse en cuanto se abra.
        // Dibuja los elementos visuales iniciales y enciende el mapa posicionándolo en una vista general.
        public AgregarEstadioView()
        {
            // Construye y acomoda todos los botones y textos diseñados en el archivo visual XAML
            InitializeComponent();
            
            // Inicializamos el controlador que se encargará de la lógica del estadio
            _controlador = new EstadioController();

            // Creamos un punto geográfico de referencia situado en el centro del país
            var centroDefault = new Location(19.4326, -99.1332);
            
            // Le ordenamos al mapa que vuele hacia ese punto central con una vista amplia
            mapaSeleccion.MoveToRegion(MapSpan.FromCenterAndRadius(centroDefault, Distance.FromKilometers(1000)));
        }

        // Esta función se activa automáticamente cuando el usuario toca cualquier punto dentro del mapa.
        // Sirve para recordar las coordenadas del sitio elegido y dibuja una pequeña marca visual.
        private void OnMapClicked(object sender, MapClickedEventArgs e)
        {
            // Guardamos en nuestra memoria interna el lugar exacto donde el usuario tocó
            _ubicacionSeleccionada = e.Location;

            // Quitamos cualquier marca que hayamos puesto anteriormente para no confundir al usuario
            mapaSeleccion.Pins.Clear();

            // Fabricamos una nueva marca roja para colocarla justo donde se tocó la pantalla
            var pin = new Pin
            {
                // Le ponemos un título amigable a la marca
                Label = "Nuevo Estadio",
                // Añadimos una descripción complementaria
                Address = "Ubicación seleccionada",
                // Indicamos que representa un lugar físico en el mundo
                Type = PinType.Place,
                // Le asignamos las coordenadas del punto tocado
                Location = _ubicacionSeleccionada
            };

            // Colocamos finalmente la marca recién creada sobre la superficie del mapa
            mapaSeleccion.Pins.Add(pin);
        }

        // Esta pequeña acción se ejecuta cuando el usuario decide no continuar con el registro.
        // Su único objetivo es cerrar la ventana actual de forma suave para regresar a la anterior.
        private async void OnCancelarClicked(object sender, EventArgs e)
        {
            // Cerramos la ventana emergente actual haciendo que desaparezca de la pantalla
            await Navigation.PopModalAsync();
        }

        // Esta función se encarga de revisar que toda la información necesaria esté completa antes de
        // mandar a guardar. Comprueba los datos, muestra una animación de espera y guarda el estadio.
        private async void OnGuardarClicked(object sender, EventArgs e)
        {
            // Revisamos si el usuario se olvidó de tocar el mapa para elegir un lugar
            if (_ubicacionSeleccionada == null)
            {
                // Le mostramos un aviso flotante pidiéndole que marque la ubicación
                await DisplayAlert("Atención", "Debes tocar el mapa para seleccionar una ubicación.", "OK");
                // Detenemos el proceso aquí mismo para que no guarde datos incompletos
                return;
            }

            // Leemos el texto que el usuario escribió en la cajita del nombre
            var nombre = txtNombreEstadio.Text?.Trim();
            
            // Verificamos si dejó la cajita del nombre totalmente vacía
            if (string.IsNullOrWhiteSpace(nombre))
            {
                // Le pedimos amablemente que escriba cómo se llama el estadio
                await DisplayAlert("Atención", "Debes ingresar el nombre del estadio.", "OK");
                // Detenemos el avance del guardado por seguridad
                return;
            }

            // Encendemos una animación visual de carga para que el usuario sepa que estamos trabajando
            overlayLoading.IsVisible = true;
            
            // Apagamos el botón de guardar para evitar que lo presionen muchas veces por error
            btnGuardar.IsEnabled = false;

            // Le pedimos al cerebro del sistema que intente guardar el estadio con su nombre y coordenadas
            var resultado = await _controlador.GuardarEstadioAsync(nombre, _ubicacionSeleccionada.Latitude, _ubicacionSeleccionada.Longitude);

            // Una vez que terminó el proceso, apagamos la pantalla de carga
            overlayLoading.IsVisible = false;
            
            // Volvemos a activar el botón de guardado para futuras acciones
            btnGuardar.IsEnabled = true;

            // Si el sistema nos avisa que el estadio se guardó perfectamente
            if (resultado.Exitoso)
            {
                // Celebramos el éxito con un mensaje amigable para el usuario
                await DisplayAlert("¡Éxito!", resultado.Mensaje, "OK");
                // Cerramos automáticamente esta ventana emergente para volver atrás
                await Navigation.PopModalAsync();
            }
            // Si ocurrió algún error técnico durante la comunicación
            else
            {
                // Le mostramos al usuario qué fue lo que falló para que lo revise
                await DisplayAlert("Error", resultado.Mensaje, "OK");
            }
        }
    }
}
