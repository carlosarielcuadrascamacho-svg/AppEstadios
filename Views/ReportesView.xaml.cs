using AppEstadios.Controllers;
using Syncfusion.Maui.Maps;

namespace AppEstadios.Views
{
    /// <summary>
    /// ReportesView — Code-behind del mapa de eventos próximos (Syncfusion Maps).
    ///
    /// ROL EN MVC (View):
    ///   - Delegar carga de datos a ReportesController.
    ///   - Crear MarkerEstadio (subclase de MapMarker de Syncfusion) con los datos del DTO.
    ///   - Manejar el tap en marcadores → mostrar Bottom Sheet con animación.
    ///   - Tap fuera de la tarjeta → ocultar con animación.
    ///   - Gestionar estados: cargando / vacío / con datos.
    /// </summary>
    public partial class ReportesView : ContentPage
    {
        // ──────────────────────────────────────────────────────────
        //  ESTADO
        // ──────────────────────────────────────────────────────────

        private readonly ReportesController _controlador;

        /// <summary>Cargamos una sola vez por sesión para no re-dibujar el mapa.</summary>
        private bool _yaCartografado = false;

        // ──────────────────────────────────────────────────────────
        //  CONSTRUCTOR
        // ──────────────────────────────────────────────────────────

        public ReportesView()
        {
            InitializeComponent();
            _controlador = new ReportesController();
        }

        // ──────────────────────────────────────────────────────────
        //  CICLO DE VIDA
        // ──────────────────────────────────────────────────────────

        protected override async void OnAppearing()
        {
            base.OnAppearing();
            if (_yaCartografado) return;
            await InicializarMapaAsync();
        }

        // ──────────────────────────────────────────────────────────
        //  INICIALIZACIÓN
        // ──────────────────────────────────────────────────────────

        private async Task InicializarMapaAsync()
        {
            overlayLoading.IsVisible = true;
            overlayVacio.IsVisible   = false;
            bottomSheet.IsVisible    = false;

            // ── El controlador ejecuta: Filtrar → Agrupar → Seleccionar → Proyectar ──
            var pines = await _controlador.ObtenerPinesEventosFuturosAsync();

            overlayLoading.IsVisible = false;

            if (pines.Count == 0)
            {
                overlayVacio.IsVisible  = true;
                lblContadorPines.Text   = "Sin eventos próximos";
                _yaCartografado         = true;
                return;
            }

            // ── Crear y registrar marcadores en Syncfusion ──
            ColocarMarcadoresEnMapa(pines);

            // ── Centrar y hacer zoom para abarcar todos los pines ──
            var region = ReportesController.CalcularRegion(pines);
            capaMapa.Center    = new MapLatLng(region.Latitud, region.Longitud);
            capaMapa.ZoomLevel = region.ZoomLevel;

            var plural = pines.Count > 1 ? "s" : "";
            lblContadorPines.Text = $"{pines.Count} estadio{plural} con próximo evento";

            _yaCartografado = true;
        }

        // ──────────────────────────────────────────────────────────
        //  MARCADORES
        // ──────────────────────────────────────────────────────────

        /// <summary>
        /// Convierte cada PinEventoInfo en un MarkerEstadio (subclase de MapMarker)
        /// y lo agrega a la colección del mapa.
        /// La MarkerTemplate del XAML enlaza {Binding Label} al NombreEstadio.
        /// </summary>
        private void ColocarMarcadoresEnMapa(List<PinEventoInfo> pines)
        {
            coleccionMarcadores.Clear();

            foreach (var pinInfo in pines)
            {
                var marcador = new MarkerEstadio
                {
                    Latitude        = pinInfo.Latitud,
                    Longitude       = pinInfo.Longitud,
                    Label           = pinInfo.NombreEstadio,
                    // Datos extra para la Bottom Sheet
                    Info            = pinInfo
                };

                coleccionMarcadores.Add(marcador);
            }
        }

        // ──────────────────────────────────────────────────────────
        //  EVENTO DE TAP EN MARCADOR
        // ──────────────────────────────────────────────────────────

        /// <summary>
        /// Syncfusion dispara este evento cuando el usuario toca un marcador.
        /// Leemos el MarkerEstadio y mostramos la Bottom Sheet con sus datos.
        /// </summary>
        private void OnMarkerClicked(object sender, MapMarkerSelectedEventArgs e)
        {
            if (e.Marker is MarkerEstadio marcador)
                MostrarBottomSheet(marcador.Info);
        }

        // ──────────────────────────────────────────────────────────
        //  BOTTOM SHEET — Mostrar / Ocultar
        // ──────────────────────────────────────────────────────────

        private void MostrarBottomSheet(PinEventoInfo info)
        {
            lblCardEstadio.Text = info.NombreEstadio;
            lblCardPartido.Text = $"{info.NombreLocal} vs {info.NombreVisitante}";
            lblCardFecha.Text   = info.FechaFormateada;
            lblCardHora.Text    = info.HoraFormateada;

            bottomSheet.Opacity      = 0;
            bottomSheet.TranslationY = 40;
            bottomSheet.IsVisible    = true;
            tapCerrarSheet.IsVisible = true;

            // Animación de entrada: slide desde abajo + fade in
            MainThread.BeginInvokeOnMainThread(async () =>
            {
                await Task.WhenAll(
                    bottomSheet.FadeTo(1, 280, Easing.CubicOut),
                    bottomSheet.TranslateTo(0, 0, 280, Easing.CubicOut)
                );
            });
        }

        private async Task OcultarBottomSheetAsync()
        {
            await Task.WhenAll(
                bottomSheet.FadeTo(0, 200, Easing.CubicIn),
                bottomSheet.TranslateTo(0, 30, 200, Easing.CubicIn)
            );
            bottomSheet.IsVisible    = false;
            tapCerrarSheet.IsVisible = false;
        }

        private async void OnMapaTapped(object? sender, Microsoft.Maui.Controls.TappedEventArgs e)
        {
            if (bottomSheet.IsVisible)
                await OcultarBottomSheetAsync();
        }
    }

    // ──────────────────────────────────────────────────────────
    //  MARCADOR PERSONALIZADO DE SYNCFUSION
    // ──────────────────────────────────────────────────────────

    /// <summary>
    /// Subclase de MapMarker que porta el PinEventoInfo completo.
    /// Permite recuperar todos los datos del evento en el handler MarkerClicked
    /// sin necesidad de hacer una segunda consulta a la BD.
    /// </summary>
    public class MarkerEstadio : MapMarker
    {
        /// <summary>Datos completos del evento asociados a este marcador.</summary>
        public PinEventoInfo Info { get; set; } = new();
    }
}