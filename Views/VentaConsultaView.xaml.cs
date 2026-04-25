using AppEstadios.Controllers;
using AppEstadios.Models;

namespace AppEstadios.Views
{
    /// <summary>
    /// VentaConsultaView — Code-behind de la pantalla de Venta de Boletos.
    ///
    /// ROL EN MVC (View):
    ///   - Cargar Picker único de eventos al abrir la pantalla.
    ///   - Mostrar la fecha del evento FUERA del picker como label.
    ///   - Actualizar los 4 KPI cards al seleccionar un evento.
    ///   - Auto-calcular el "Total a Pagar" al escribir la cantidad.
    ///   - Delegar guardar/eliminar al controlador.
    /// </summary>
    public partial class VentaConsultaView : ContentPage
    {
        // ──────────────────────────────────────────────────────────
        //  ESTADO
        // ──────────────────────────────────────────────────────────

        private readonly VentaConsultaController _controlador;

        /// <summary>Evento real actualmente seleccionado.</summary>
        private Evento? _eventoSeleccionado;

        /// <summary>Estadísticas del evento (para auto-cálculo y validación).</summary>
        private EstadisticasEvento? _estadisticasActuales;

        // ──────────────────────────────────────────────────────────
        //  CONSTRUCTOR
        // ──────────────────────────────────────────────────────────

        public VentaConsultaView()
        {
            InitializeComponent();
            _controlador = new VentaConsultaController();

            // Suscribimos el handler del Picker único de eventos
            pickerEvento.SelectedIndexChanged += OnEventoSeleccionado;

            ConfigurarAnimacionesInteractivas();
        }

        private void ConfigurarAnimacionesInteractivas()
        {
            // Efecto Scale en el botón de confirmar (Feedback inmediato)
            btnConfirmar.Pressed += async (s, e) => await btnConfirmar.ScaleTo(0.95, 100, Easing.SinOut);
            btnConfirmar.Released += async (s, e) => await btnConfirmar.ScaleTo(1.0, 100, Easing.SinOut);
        }

        // ──────────────────────────────────────────────────────────
        //  CICLO DE VIDA
        // ──────────────────────────────────────────────────────────

        protected override async void OnAppearing()
        {
            base.OnAppearing();

            // Preparar elementos para animación de entrada
            DashboardSection.Opacity = 0;
            DashboardSection.TranslationY = 30;
            FormSection.Opacity = 0;
            FormSection.TranslationY = 30;
            BotonesSection.Opacity = 0;
            BotonesSection.TranslationY = 30;

            AnimarEntradaAsync();

            await CargarEventosAsync();
        }

        private async void AnimarEntradaAsync()
        {
            await Task.Delay(100);
            _ = Task.WhenAll(
                DashboardSection.FadeTo(1, 400, Easing.CubicOut),
                DashboardSection.TranslateTo(0, 0, 400, Easing.CubicOut)
            );

            await Task.Delay(100);
            _ = Task.WhenAll(
                FormSection.FadeTo(1, 400, Easing.CubicOut),
                FormSection.TranslateTo(0, 0, 400, Easing.CubicOut)
            );

            await Task.Delay(100);
            _ = Task.WhenAll(
                BotonesSection.FadeTo(1, 400, Easing.CubicOut),
                BotonesSection.TranslateTo(0, 0, 400, Easing.CubicOut)
            );
        }

        // ──────────────────────────────────────────────────────────
        //  CARGA DE DATOS
        // ──────────────────────────────────────────────────────────

        /// <summary>
        /// Carga la lista de EventoDisplay en el Picker.
        /// Cada item muestra "América vs Chivas  •  Estadio Azteca" (sin fecha).
        /// </summary>
        private async Task CargarEventosAsync()
        {
            _eventoSeleccionado = null;
            ActualizarEstadisticasEnUI(null);
            OcultarFecha();

            var eventos = await _controlador.ObtenerEventosConDetalleAsync();
            pickerEvento.ItemsSource = eventos;

            if (eventos.Count == 0)
                await DisplayAlert("Sin partidos",
                    "No hay partidos registrados. Ve a 'Registro' para crear uno.", "OK");
        }

        // ──────────────────────────────────────────────────────────
        //  PICKER — Selección de evento
        // ──────────────────────────────────────────────────────────

        /// <summary>
        /// Al seleccionar un evento del Picker:
        ///   1. Muestra la fecha del partido en el label independiente (bajo el picker).
        ///   2. Carga y muestra los 4 KPIs.
        ///   3. Resetea el formulario y el cálculo automático.
        /// </summary>
        private async void OnEventoSeleccionado(object? sender, EventArgs e)
        {
            if (pickerEvento.SelectedItem is not EventoDisplay seleccion)
                return;

            // Guardamos el evento real
            _eventoSeleccionado = seleccion.Evento;

            // ── Mostrar la fecha FUERA del picker ──
            lblFechaEvento.Text     = seleccion.FechaFormateada;
            lblFechaEvento.IsVisible   = true;

            // ── Cargar estadísticas y actualizar los 4 KPIs ──
            _estadisticasActuales = await _controlador.ObtenerEstadisticasAsync(_eventoSeleccionado);
            ActualizarEstadisticasEnUI(_estadisticasActuales);

            // ── Mostrar el botón Eliminar ──
            btnEliminarContainer.IsVisible = true;

            // ── Resetear formulario y cálculo ──
            LimpiarFormularioVenta();
        }

        // ──────────────────────────────────────────────────────────
        //  ACTUALIZAR KPIs EN UI
        // ──────────────────────────────────────────────────────────

        private void AnimarContador(Label label, double valorFinal, bool esMoneda)
        {
            label.AbortAnimation("Contador");
            var animacion = new Animation(v =>
            {
                label.Text = esMoneda ? $"${v:F2}" : Math.Floor(v).ToString();
            }, 0, valorFinal, Easing.CubicOut);

            animacion.Commit(label, "Contador", length: 400);
        }

        /// <summary>
        /// Actualiza los 4 cards de KPIs.
        /// Si stats es null → muestra valores por defecto (estado vacío).
        /// </summary>
        private void ActualizarEstadisticasEnUI(EstadisticasEvento? stats)
        {
            if (stats is null)
            {
                lblTotalBoletos.Text           = "—";
                lblRecaudado.Text              = "$0.00";
                lblDisponibles.Text            = "—";
                lblPrecioBoleto.Text           = "$0.00";
                btnEliminarContainer.IsVisible = false;
            }
            else
            {
                AnimarContador(lblTotalBoletos, stats.TotalBoletos, false);
                AnimarContador(lblRecaudado, (double)stats.TotalRecaudado, true);
                AnimarContador(lblDisponibles, stats.BoletosDisponibles, false);
                lblPrecioBoleto.Text = $"${stats.PrecioPorBoleto:F2}";
            }

            // Siempre reseteamos el total a pagar al cambiar de evento
            lblTotalPagar.Text = "$0.00";
        }

        // ──────────────────────────────────────────────────────────
        //  AUTO-CÁLCULO DEL TOTAL A PAGAR
        // ──────────────────────────────────────────────────────────

        /// <summary>
        /// Se dispara cada vez que el usuario escribe en el campo de cantidad.
        /// Calcula automáticamente: Total = Cantidad × Precio por Boleto.
        /// Si la cantidad no es válida, muestra $0.00.
        /// </summary>
        private void OnCantBoletosTextChanged(object sender, TextChangedEventArgs e)
        {
            // No calculamos si no hay evento seleccionado o sin precio
            if (_estadisticasActuales is null || _estadisticasActuales.PrecioPorBoleto <= 0)
            {
                lblTotalPagar.Text = "$0.00";
                return;
            }

            // Parseamos la cantidad ingresada
            if (int.TryParse(e.NewTextValue, out int cantidad) && cantidad > 0)
            {
                var total = cantidad * _estadisticasActuales.PrecioPorBoleto;
                lblTotalPagar.Text = $"${total:F2}";

                // Efecto visual leve de feedback
                _ = lblTotalPagar.ScaleTo(1.1, 100, Easing.CubicOut).ContinueWith(t => lblTotalPagar.ScaleTo(1.0, 100, Easing.CubicOut));
            }
            else
            {
                lblTotalPagar.Text = "$0.00";
            }
        }

        // ──────────────────────────────────────────────────────────
        //  BOTONES
        // ──────────────────────────────────────────────────────────

        /// <summary>
        /// Registra una nueva venta. El controlador valida y guarda en Supabase.
        /// Tras éxito, refresca los KPIs sin recargar el Picker.
        /// </summary>
        private async void OnGuardarVentaClicked(object sender, EventArgs e)
        {
            var boton = (Button)sender;
            boton.IsEnabled = false;

            var resultado = await _controlador.GuardarVentaAsync(
                _eventoSeleccionado,
                txtNombreCliente.Text?.Trim() ?? string.Empty,
                txtTelefono.Text?.Trim()      ?? string.Empty,
                txtCantBoletos.Text?.Trim()   ?? string.Empty,
                _estadisticasActuales);

            if (resultado.Exitoso)
            {
                // Animación de éxito temporal (Feedback visual)
                _ = btnConfirmar.ScaleTo(1.05, 100, Easing.CubicOut).ContinueWith(t => btnConfirmar.ScaleTo(1.0, 100, Easing.CubicOut));
                var colorOriginal = btnConfirmar.BackgroundColor;
                btnConfirmar.BackgroundColor = Color.FromArgb("#10B981");
                
                await Task.Delay(300);
                btnConfirmar.BackgroundColor = colorOriginal;

                await DisplayAlert("¡Venta Registrada! 🎟️", resultado.Mensaje, "Aceptar");

                // Refrescamos SOLO los KPIs (sin recargar el Picker completo)
                if (_eventoSeleccionado is not null)
                {
                    _estadisticasActuales = await _controlador.ObtenerEstadisticasAsync(_eventoSeleccionado);
                    ActualizarEstadisticasEnUI(_estadisticasActuales);
                }

                LimpiarFormularioVenta();
            }
            else
            {
                await DisplayAlert("⚠️ Atención", resultado.Mensaje, "Entendido");
            }

            boton.IsEnabled = true;
        }

        /// <summary>Limpia el formulario sin guardar.</summary>
        private void OnCancelarClicked(object sender, EventArgs e)
        {
            LimpiarFormularioVenta();
        }

        /// <summary>
        /// Elimina el evento seleccionado con confirmación explícita.
        /// </summary>
        private async void OnEliminarEventoClicked(object sender, EventArgs e)
        {
            if (_eventoSeleccionado is null) return;

            bool confirmar = await DisplayAlert(
                "⚠️ Confirmar Eliminación",
                "¿Eliminar este partido y TODAS sus ventas?\nEsta acción no se puede deshacer.",
                "Sí, eliminar",
                "Cancelar");

            if (!confirmar) return;

            var boton = (Button)sender;
            boton.IsEnabled = false;

            var resultado = await _controlador.EliminarEventoAsync(_eventoSeleccionado);

            if (resultado.Exitoso)
            {
                await DisplayAlert("Eliminado ✅", resultado.Mensaje, "OK");

                // Recargamos todo desde cero
                _eventoSeleccionado        = null;
                pickerEvento.SelectedIndex = -1;
                await CargarEventosAsync();
            }
            else
            {
                await DisplayAlert("Error", resultado.Mensaje, "OK");
                boton.IsEnabled = true;
            }
        }

        // ──────────────────────────────────────────────────────────
        //  AUXILIARES
        // ──────────────────────────────────────────────────────────

        /// <summary>Limpia los campos del formulario y resetea el total calculado.</summary>
        private void LimpiarFormularioVenta()
        {
            txtNombreCliente.Text = string.Empty;
            txtTelefono.Text      = string.Empty;
            txtCantBoletos.Text   = string.Empty;
            lblTotalPagar.Text    = "$0.00";
        }

        /// <summary>Oculta el label de fecha y el botón eliminar.</summary>
        private void OcultarFecha()
        {
            lblFechaEvento.IsVisible       = false;
            lblFechaEvento.Text            = "—";
            btnEliminarContainer.IsVisible = false;
        }
    }
}