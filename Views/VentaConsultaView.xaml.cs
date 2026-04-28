// Traemos el cerebro que maneja el proceso de cobros y consultas
using AppEstadios.Controllers;
// Traemos los modelos con los datos básicos de Eventos y Ventas
using AppEstadios.Models;

namespace AppEstadios.Views
{
    // Esta pantalla es la caja registradora y el tablero de control de los partidos de fútbol.
    // Permite revisar cuántos asientos quedan libres en cada estadio, cuánto dinero hemos
    // cobrado en total y procesar compras de boletos cobrándole a los clientes.
    public partial class VentaConsultaView : ContentPage
    {
        // Guardamos la referencia al controlador encargado de las ventas
        private readonly VentaConsultaController _controlador;

        // Guardamos la información del partido que se está operando en este instante
        private Evento? _eventoSeleccionado;

        // Llevamos la cuenta de los boletos y ganancias del partido en pantalla
        private EstadisticasEvento? _estadisticasActuales;

        // Prepara los controles de la interfaz visual al iniciar y conecta las acciones
        // de la lista desplegable con el controlador adecuado.
        public VentaConsultaView()
        {
            // Ensamblamos todos los botones y menús del diseño visual XAML
            InitializeComponent();
            
            // Inicializamos el controlador de venta
            _controlador = new VentaConsultaController();

            // Le indicamos qué hacer cuando el usuario cambie de partido en la lista
            pickerEvento.SelectedIndexChanged += OnEventoSeleccionado;

            // Añadimos interactividad visual en los botones principales
            ConfigurarAnimacionesInteractivas();
        }

        // Añade efectos visuales como encoger levemente el botón de confirmación
        // para asegurar que la pulsación del usuario sea detectada.
        private void ConfigurarAnimacionesInteractivas()
        {
            // Achicamos el botón al poner el dedo encima
            btnConfirmar.Pressed += async (s, e) => await btnConfirmar.ScaleTo(0.95, 100, Easing.SinOut);
            // Devolvemos su tamaño original al retirar el dedo
            btnConfirmar.Released += async (s, e) => await btnConfirmar.ScaleTo(1.0, 100, Easing.SinOut);
        }

        // Se ejecutan al abrir la pantalla para mover sutilmente los bloques de
        // información y cargar los partidos guardados en el servidor.
        protected override async void OnAppearing()
        {
            // Respetamos el comportamiento normal del teléfono al abrir la vista
            base.OnAppearing();

            // Ocultamos los tres grandes bloques del formulario para animarlos
            DashboardSection.Opacity = 0;
            DashboardSection.TranslationY = 30;
            FormSection.Opacity = 0;
            FormSection.TranslationY = 30;
            BotonesSection.Opacity = 0;
            BotonesSection.TranslationY = 30;

            // Lanzamos los movimientos de aparición en pantalla
            AnimarEntradaAsync();

            // Pedimos los partidos de fútbol organizados al sistema
            await CargarEventosAsync();
        }

        // Ejecuta el efecto escalonado para revelar las secciones de la aplicación.
        private async void AnimarEntradaAsync()
        {
            // Esperamos una fracción mínima de tiempo para iniciar
            await Task.Delay(100);
            // Hacemos emerger el tablero de ganancias y estadísticas
            _ = Task.WhenAll(
                DashboardSection.FadeTo(1, 400, Easing.CubicOut),
                DashboardSection.TranslateTo(0, 0, 400, Easing.CubicOut)
            );

            await Task.Delay(100);
            // Levantamos suavemente el formulario de compra
            _ = Task.WhenAll(
                FormSection.FadeTo(1, 400, Easing.CubicOut),
                FormSection.TranslateTo(0, 0, 400, Easing.CubicOut)
            );

            await Task.Delay(100);
            // Hacemos visible el panel de botones finales
            _ = Task.WhenAll(
                BotonesSection.FadeTo(1, 400, Easing.CubicOut),
                BotonesSection.TranslateTo(0, 0, 400, Easing.CubicOut)
            );
        }

        // Pide la lista de partidos organizados al sistema y la coloca dentro de la caja
        // de selección principal para que el usuario elija cuál evento operar.
        private async Task CargarEventosAsync()
        {
            // Limpiamos cualquier selección anterior para no mezclar datos
            _eventoSeleccionado = null;
            ActualizarEstadisticasEnUI(null);
            OcultarFecha();
            OcultarBannerEstado();

            // Reiniciamos el menú desplegable por completo
            pickerEvento.SelectedIndex = -1;
            pickerEvento.ItemsSource   = null;

            // Buscamos todos los eventos disponibles en el sistema
            var eventos = await _controlador.ObtenerEventosConDetalleAsync();
            pickerEvento.ItemsSource = eventos;

            // Si la base de datos no arrojó ningún partido, avisamos al operador
            if (eventos.Count == 0)
                await DisplayAlert("Sin partidos",
                    "No hay partidos registrados. Ve a 'Registro' para crear uno.", "OK");
        }

        // Se activa al escoger un partido. Pone la fecha a la vista de todos, calcula las
        // ganancias acumuladas y deshabilita la compra si el juego ya pasó o está agotado.
        private async void OnEventoSeleccionado(object? sender, EventArgs e)
        {
            // Nos aseguramos de que la selección del usuario sea válida
            if (pickerEvento.SelectedItem is not EventoDisplay seleccion)
                return;

            // Guardamos el partido concreto que se escogió
            _eventoSeleccionado = seleccion.Evento;

            // Pintamos el día y la hora del partido en la interfaz
            lblFechaEvento.Text     = seleccion.FechaFormateada;
            lblFechaEvento.IsVisible   = true;

            // Solicitamos calcular el dinero acumulado y boletos que restan
            _estadisticasActuales = await _controlador.ObtenerEstadisticasAsync(_eventoSeleccionado);
            ActualizarEstadisticasEnUI(_estadisticasActuales);

            // Evaluamos si el partido sigue abierto al público para comprar boletos
            AplicarEstadoDisponibilidad(_eventoSeleccionado, _estadisticasActuales);

            // Dejamos en blanco el espacio del comprador para atender a un cliente nuevo
            LimpiarFormularioVenta();
        }

        // Crea una ilusión visual donde los números de dinero y boletos van subiendo
        // rápidamente hasta alcanzar la cifra final en lugar de saltar de golpe.
        private void AnimarContador(Label label, double valorFinal, bool esMoneda)
        {
            // Cancelamos animaciones anteriores que estuvieran corriendo en el letrero
            label.AbortAnimation("Contador");
            // Programamos el aumento paulatino del valor numérico
            var animacion = new Animation(v =>
            {
                // Le damos formato de billetes o de piezas enteras según corresponda
                label.Text = esMoneda ? $"${v:F2}" : Math.Floor(v).ToString();
            }, 0, valorFinal, Easing.CubicOut);

            // Arrancamos el contador visual con una duración de casi medio segundo
            animacion.Commit(label, "Contador", length: 400);
        }

        // Dibuja en pantalla los valores finales de dinero cobrado, boletos restantes y precios.
        private void ActualizarEstadisticasEnUI(EstadisticasEvento? stats)
        {
            // Si no tenemos datos del partido (estado inicial vacío)
            if (stats is null)
            {
                lblTotalBoletos.Text           = "—";
                lblRecaudado.Text              = "$0.00";
                lblDisponibles.Text            = "—";
                lblPrecioBoleto.Text           = "$0.00";

                // Mejora #8: Reiniciamos la barra de progreso al estado vacío
                progBoletos.Progress       = 0;
                lblPorcentajeVenta.Text    = "0%";
                lblHintBoletos.Text        = string.Empty;
            }
            // Si logramos recuperar las matemáticas del estadio
            else
            {
                AnimarContador(lblTotalBoletos, stats.TotalBoletos, false);
                AnimarContador(lblRecaudado, (double)stats.TotalRecaudado, true);
                AnimarContador(lblDisponibles, stats.BoletosDisponibles, false);
                lblPrecioBoleto.Text = $"${stats.PrecioPorBoleto:F2}";

                // Mejora #8: Calculamos el porcentaje de boletos vendidos para la barra visual
                double vendidos  = stats.TotalBoletos - stats.BoletosDisponibles;
                double porcentaje = stats.TotalBoletos > 0
                    ? vendidos / stats.TotalBoletos
                    : 0;
                progBoletos.ProgressTo(porcentaje, 600, Easing.CubicOut);
                lblPorcentajeVenta.Text = $"{porcentaje * 100:F0}%";
                lblPorcentajeVenta.TextColor = porcentaje >= 0.9
                    ? Color.FromArgb("#EF4444") // Rojo si casi agotado
                    : Color.FromArgb("#10B981"); // Verde normal

                // Mejora #4: Mostramos los boletos disponibles debajo del campo de cantidad
                lblHintBoletos.Text = stats.BoletosDisponibles > 0
                    ? $"Disponibles: {stats.BoletosDisponibles}"
                    : "Sin boletos disponibles";
                lblHintBoletos.TextColor = stats.BoletosDisponibles > 0
                    ? Color.FromArgb("#6B7280")
                    : Color.FromArgb("#EF4444");
            }

            // Reiniciamos el cálculo del cobro cada vez que el usuario cambie de partido
            lblTotalPagar.Text = "$0.00";
        }

        // Mejora #3: Actualiza el contador de dígitos del teléfono en tiempo real con color.
        private void OnTelefonoTextChanged(object sender, TextChangedEventArgs e)
        {
            var digitos = new string((e.NewTextValue ?? string.Empty).Where(char.IsDigit).ToArray()).Length;
            lblContadorTelefono.Text = $"{digitos} / 10 dígitos";
            lblContadorTelefono.TextColor = digitos == 10
                ? Color.FromArgb("#10B981") // Verde cuando está completo
                : Color.FromArgb("#6B7280"); // Gris mientras falta
        }

        // Multiplica la cantidad de boletos que el cliente desea comprar por el precio
        // individual, mostrando el monto a pagar en la caja de forma automática.
        private void OnCantBoletosTextChanged(object sender, TextChangedEventArgs e)
        {
            // Cancelamos el cobro si el partido no tiene precio fijado
            if (_estadisticasActuales is null || _estadisticasActuales.PrecioPorBoleto <= 0)
            {
                lblTotalPagar.Text = "$0.00";
                return;
            }

            // Si el usuario escribió un número positivo de boletos
            if (int.TryParse(e.NewTextValue, out int cantidad) && cantidad > 0)
            {
                // Multiplicamos piezas por costo unitario
                var total = cantidad * _estadisticasActuales.PrecioPorBoleto;
                lblTotalPagar.Text = $"${total:F2}";

                // Hacemos brincar ligeramente el precio para llamar la atención del vendedor
                _ = lblTotalPagar.ScaleTo(1.1, 100, Easing.CubicOut).ContinueWith(t => lblTotalPagar.ScaleTo(1.0, 100, Easing.CubicOut));
            }
            // Si escribieron letras o dejaron el espacio en blanco
            else
            {
                lblTotalPagar.Text = "$0.00";
            }
        }

        // Procesa y guarda la venta de boletos enviando los datos del cliente al servidor
        // para actualizar los contadores generales de asistencia y dinero.
        private async void OnGuardarVentaClicked(object sender, EventArgs e)
        {
            // Transformamos el remitente al botón original
            var boton = (Button)sender;

            // Mejora #7: Confirmación previa con resumen antes de guardar
            var nombre   = txtNombreCliente.Text?.Trim() ?? string.Empty;
            var telefono = txtTelefono.Text?.Trim()      ?? string.Empty;
            var cantText = txtCantBoletos.Text?.Trim()   ?? string.Empty;

            // Solo mostramos el resumen si hay datos suficientes para armar el mensaje
            if (!string.IsNullOrWhiteSpace(nombre) && int.TryParse(cantText, out int cantPrev) && cantPrev > 0)
            {
                var totalPrev = _estadisticasActuales is not null
                    ? cantPrev * _estadisticasActuales.PrecioPorBoleto
                    : 0;

                bool confirmar = await DisplayAlert(
                    "Confirmar Venta",
                    $"👤 Cliente: {nombre}\n" +
                    $"🎫 Boletos: {cantPrev}\n" +
                    $"💰 Total: ${totalPrev:F2}\n\n" +
                    $"¿Deseas registrar esta venta?",
                    "Sí, confirmar",
                    "Revisar");

                if (!confirmar) return;
            }

            // Lo deshabilitamos para evitar pedidos duplicados
            boton.IsEnabled = false;

            // Mandamos toda la compra hacia el controlador para que la guarde formalmente
            var resultado = await _controlador.GuardarVentaAsync(
                _eventoSeleccionado,
                txtNombreCliente.Text?.Trim() ?? string.Empty,
                txtTelefono.Text?.Trim()      ?? string.Empty,
                txtCantBoletos.Text?.Trim()   ?? string.Empty,
                _estadisticasActuales);

            // Si el servidor aceptó el registro de la compra
            if (resultado.Exitoso)
            {
                // Dibujamos un destello verde festivo en el botón de confirmación
                _ = btnConfirmar.ScaleTo(1.05, 100, Easing.CubicOut).ContinueWith(t => btnConfirmar.ScaleTo(1.0, 100, Easing.CubicOut));
                var colorOriginal = btnConfirmar.BackgroundColor;
                btnConfirmar.BackgroundColor = Color.FromArgb("#10B981");
                
                await Task.Delay(300);
                btnConfirmar.BackgroundColor = colorOriginal;

                // Lanzamos una alerta confirmando los asientos comprados
                await DisplayAlert("¡Venta Registrada! 🎟️", resultado.Mensaje, "Aceptar");

                // Si tenemos el partido seleccionado en la mira
                if (_eventoSeleccionado is not null)
                {
                    // Traemos los KPIs financieros refrescados para actualizar el tablero
                    _estadisticasActuales = await _controlador.ObtenerEstadisticasAsync(_eventoSeleccionado);
                    ActualizarEstadisticasEnUI(_estadisticasActuales);
                }

                // Limpiamos los campos de texto para recibir al siguiente comprador
                LimpiarFormularioVenta();
            }
            // En caso de que la transacción haya sido rechazada por falta de cupo
            else
            {
                await DisplayAlert("⚠️ Atención", resultado.Mensaje, "Entendido");
            }

            // Volvemos a encender el botón
            boton.IsEnabled = true;
        }

        // Limpia los cuadros de texto borrando el nombre del cliente si se cancela la venta.
        private void OnCancelarClicked(object sender, EventArgs e)
        {
            LimpiarFormularioVenta();
        }


        // Limpia los campos del formulario y resetea el total calculado.
        private void LimpiarFormularioVenta()
        {
            txtNombreCliente.Text = string.Empty;
            txtTelefono.Text      = string.Empty;
            txtCantBoletos.Text   = string.Empty;
            lblTotalPagar.Text    = "$0.00";
        }

        // Oculta el letrero de fecha y el botón de eliminar.
        private void OcultarFecha()
        {
            lblFechaEvento.IsVisible       = false;
            lblFechaEvento.Text            = "—";
        }

        // Habilita o deshabilita el formulario de venta según las reglas de disponibilidad.
        private void AplicarEstadoDisponibilidad(Evento evento, EstadisticasEvento stats)
        {
            // Evaluamos las restricciones de tiempo y cupo del partido
            var (disponible, motivo) = _controlador.VerificarDisponibilidadVenta(evento, stats);

            // Encendemos o apagamos los botones de compra según el resultado
            FormSection.IsEnabled  = disponible;
            btnConfirmar.IsEnabled = disponible;

            // Aplicamos una opacidad opaca para avisar que el acceso está bloqueado
            FormSection.Opacity  = disponible ? 1.0 : 0.45;
            btnConfirmar.Opacity = disponible ? 1.0 : 0.45;

            // Si las ventas están cerradas y tenemos un motivo claro
            if (!disponible && motivo is not null)
            {
                // Escribimos la razón del bloqueo en el banner visual
                lblEstadoVenta.Text   = motivo;
                bannerEstado.IsVisible = true;
            }
            // Si la venta está permitida, escondemos el banner de alerta
            else
            {
                OcultarBannerEstado();
            }
        }

        // Devuelve el formulario de compras a su brillo y funcionalidad por defecto.
        private void OcultarBannerEstado()
        {
            bannerEstado.IsVisible = false;
            lblEstadoVenta.Text    = string.Empty;
            FormSection.IsEnabled  = true;
            FormSection.Opacity    = 1.0;
            btnConfirmar.IsEnabled = true;
            btnConfirmar.Opacity   = 1.0;
        }
    }
}