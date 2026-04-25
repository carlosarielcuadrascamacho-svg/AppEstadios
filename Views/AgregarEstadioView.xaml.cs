using AppEstadios.Controllers;
using Microsoft.Maui.Controls.Maps;
using Microsoft.Maui.Maps;

namespace AppEstadios.Views
{
    public partial class AgregarEstadioView : ContentPage
    {
        private readonly EstadioController _controlador;
        private Location? _ubicacionSeleccionada;

        public AgregarEstadioView()
        {
            InitializeComponent();
            _controlador = new EstadioController();

            // Centrar inicialmente en una ubicación por defecto (ej. Centro de México)
            var centroDefault = new Location(19.4326, -99.1332);
            mapaSeleccion.MoveToRegion(MapSpan.FromCenterAndRadius(centroDefault, Distance.FromKilometers(1000)));
        }

        private void OnMapClicked(object sender, MapClickedEventArgs e)
        {
            _ubicacionSeleccionada = e.Location;

            // Limpiar pines anteriores
            mapaSeleccion.Pins.Clear();

            // Agregar nuevo pin
            var pin = new Pin
            {
                Label = "Nuevo Estadio",
                Address = "Ubicación seleccionada",
                Type = PinType.Place,
                Location = _ubicacionSeleccionada
            };

            mapaSeleccion.Pins.Add(pin);
        }

        private async void OnCancelarClicked(object sender, EventArgs e)
        {
            await Navigation.PopModalAsync();
        }

        private async void OnGuardarClicked(object sender, EventArgs e)
        {
            if (_ubicacionSeleccionada == null)
            {
                await DisplayAlert("Atención", "Debes tocar el mapa para seleccionar una ubicación.", "OK");
                return;
            }

            var nombre = txtNombreEstadio.Text?.Trim();
            if (string.IsNullOrWhiteSpace(nombre))
            {
                await DisplayAlert("Atención", "Debes ingresar el nombre del estadio.", "OK");
                return;
            }

            overlayLoading.IsVisible = true;
            btnGuardar.IsEnabled = false;

            var resultado = await _controlador.GuardarEstadioAsync(nombre, _ubicacionSeleccionada.Latitude, _ubicacionSeleccionada.Longitude);

            overlayLoading.IsVisible = false;
            btnGuardar.IsEnabled = true;

            if (resultado.Exitoso)
            {
                await DisplayAlert("¡Éxito!", resultado.Mensaje, "OK");
                await Navigation.PopModalAsync();
            }
            else
            {
                await DisplayAlert("Error", resultado.Mensaje, "OK");
            }
        }
    }
}
