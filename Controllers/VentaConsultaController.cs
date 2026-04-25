using AppEstadios.Models;
using static Postgrest.Constants;

namespace AppEstadios.Controllers
{
    /// <summary>
    /// VentaConsultaController — Controlador de la pantalla de Venta de Boletos.
    ///
    /// RESPONSABILIDADES (MVC):
    ///   1. Cargar todos los eventos con sus nombres de equipos y estadio
    ///      para mostrar en un único Picker con formato "Local vs Visitante - Estadio".
    ///   2. Calcular estadísticas del evento seleccionado (boletos disponibles, recaudado).
    ///   3. Validar e insertar una nueva Venta en Supabase.
    ///   4. Eliminar un evento y sus ventas asociadas (con CASCADE en BD).
    /// </summary>
    public class VentaConsultaController
    {
        // ══════════════════════════════════════════════════════════
        //  SECCIÓN 1 — CARGA DE EVENTOS CON DETALLE COMPLETO
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Carga todos los eventos y resuelve los IDs a nombres reales,
        /// construyendo el texto descriptivo para el Picker en formato:
        ///   "América vs Chivas - Estadio Azteca"
        ///
        /// ESTRATEGIA: Dado que supabase-csharp v0.16.2 no soporta JOINs,
        /// hacemos 3 consultas en paralelo y resolvemos los nombres en memoria.
        /// Esto es eficiente para catálogos pequeños (equipos/estadios).
        ///
        /// RETORNA: Lista de EventoDisplay lista para asignar al Picker.
        /// </summary>
        public async Task<List<EventoDisplay>> ObtenerEventosConDetalleAsync()
        {
            try
            {
                // ── Paso 1: Lanzamos las 3 consultas en paralelo ──
                var tareaEventos   = SupabaseService.Cliente.From<Evento>()
                                        .Order("fecha_hora_partido", Ordering.Ascending)
                                        .Get();
                var tareaEquipos   = SupabaseService.Cliente.From<Equipo>().Get();
                var tareaEstadios  = SupabaseService.Cliente.From<Estadio>().Get();

                await Task.WhenAll(tareaEventos, tareaEquipos, tareaEstadios);

                // ── Paso 2: Convertimos listas a diccionarios para búsqueda O(1) ──
                var dicEquipos  = tareaEquipos.Result.Models
                                    .ToDictionary(e => e.Id, e => e.Nombre);
                var dicEstadios = tareaEstadios.Result.Models
                                    .ToDictionary(e => e.Id, e => e.Nombre);

                // ── Paso 3: Construimos los objetos de display ──
                var resultado = new List<EventoDisplay>();

                foreach (var evento in tareaEventos.Result.Models)
                {
                    // Resolvemos los nombres (usamos "?" si no se encuentra el ID)
                    var nombreLocal      = dicEquipos.GetValueOrDefault(evento.LocalId,     "Equipo ?");
                    var nombreVisitante  = dicEquipos.GetValueOrDefault(evento.VisitanteId, "Equipo ?");
                    var nombreEstadio    = dicEstadios.GetValueOrDefault(evento.EstadioId,  "Estadio ?");

                    // Formateamos la fecha para mostrar en el picker
                    var fechaFormateada = evento.FechaHoraPartido.ToLocalTime()
                                                .ToString("dd/MM/yy HH:mm");

                    resultado.Add(new EventoDisplay
                    {
                        Evento         = evento,
                        // Solo equipo vs equipo + estadio (sin fecha) para que el texto sea corto
                        Descripcion    = $"{nombreLocal} vs {nombreVisitante}  •  {nombreEstadio}",
                        // La fecha se muestra FUERA del picker como label independiente
                        FechaFormateada= evento.FechaHoraPartido.ToLocalTime()
                                                .ToString("dddd dd/MM/yyyy  \u2022  HH:mm \"hrs\"")
                    });
                }

                return resultado;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[VentaConsultaController] Error al cargar eventos: {ex.Message}");
                return new List<EventoDisplay>();
            }
        }

        // ══════════════════════════════════════════════════════════
        //  SECCIÓN 2 — ESTADÍSTICAS DEL EVENTO
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Calcula las estadísticas actuales de un evento:
        ///   - Boletos disponibles (total - vendidos)
        ///   - Total recaudado (suma de ventas)
        ///
        /// RETORNA: Objeto EstadisticasEvento con los datos calculados.
        /// </summary>
        public async Task<EstadisticasEvento> ObtenerEstadisticasAsync(Evento evento)
        {
            try
            {
                var respuestaVentas = await SupabaseService.Cliente
                    .From<Venta>()
                    .Filter("evento_id", Operator.Equals, evento.Id.ToString())
                    .Get();

                var ventas = respuestaVentas.Models;

                int     totalVendidos  = ventas.Sum(v => v.CantidadBoletos);
                decimal totalRecaudado = ventas.Sum(v => v.TotalCobrado);
                int     disponibles    = evento.TotalBoletos - totalVendidos;

                return new EstadisticasEvento
                {
                    TotalBoletos       = evento.TotalBoletos,
                    BoletosDisponibles = disponibles,
                    TotalRecaudado     = totalRecaudado,
                    PrecioPorBoleto    = evento.PrecioBoleto
                };
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[VentaConsultaController] Error al calcular estadísticas: {ex.Message}");
                return new EstadisticasEvento
                {
                    TotalBoletos       = evento.TotalBoletos,
                    BoletosDisponibles = 0,
                    TotalRecaudado     = 0,
                    PrecioPorBoleto    = evento.PrecioBoleto
                };
            }
        }

        // ══════════════════════════════════════════════════════════
        //  SECCIÓN 3 — GUARDAR NUEVA VENTA
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Valida e inserta una nueva venta en Supabase.
        /// </summary>
        public async Task<ResultadoOperacion> GuardarVentaAsync(
            Evento? evento,
            string nombreCliente,
            string telefono,
            string cantBoletosTexto,
            EstadisticasEvento? estadisticas)
        {
            if (evento is null)
                return new ResultadoOperacion(false, "Selecciona un evento primero.");

            if (string.IsNullOrWhiteSpace(nombreCliente))
                return new ResultadoOperacion(false, "El nombre del cliente es obligatorio.");

            if (!int.TryParse(cantBoletosTexto, out int cantidad) || cantidad <= 0)
                return new ResultadoOperacion(false, "La cantidad de boletos debe ser mayor a cero.");

            int disponibles = estadisticas?.BoletosDisponibles ?? 0;
            if (cantidad > disponibles)
                return new ResultadoOperacion(false,
                    $"No hay suficientes boletos. Disponibles: {disponibles}.");

            decimal totalCobrado = cantidad * evento.PrecioBoleto;

            var nuevaVenta = new Venta
            {
                EventoId        = evento.Id,
                NombreCliente   = nombreCliente.Trim(),
                Telefono        = telefono.Trim(),
                CantidadBoletos = cantidad,
                TotalCobrado    = totalCobrado,
                FechaVenta      = DateTime.UtcNow
            };

            try
            {
                await SupabaseService.Cliente.From<Venta>().Insert(nuevaVenta);

                return new ResultadoOperacion(
                    true,
                    $"✅ Venta registrada exitosamente.\n" +
                    $"Cliente: {nuevaVenta.NombreCliente}\n" +
                    $"Boletos: {cantidad}   Total: ${totalCobrado:F2}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[VentaConsultaController] Error al guardar venta: {ex.Message}");
                return new ResultadoOperacion(false,
                    "Error al registrar la venta. Verifica tu conexión e intenta de nuevo.");
            }
        }

        // ══════════════════════════════════════════════════════════
        //  SECCIÓN 4 — ELIMINAR EVENTO
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Elimina un evento. La BD elimina las ventas en cascada (ON DELETE CASCADE).
        /// </summary>
        public async Task<ResultadoOperacion> EliminarEventoAsync(Evento? evento)
        {
            if (evento is null)
                return new ResultadoOperacion(false, "No hay evento seleccionado para eliminar.");

            try
            {
                await SupabaseService.Cliente
                    .From<Evento>()
                    .Filter("id", Operator.Equals, evento.Id.ToString())
                    .Delete();

                return new ResultadoOperacion(true, "Evento eliminado correctamente.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[VentaConsultaController] Error al eliminar evento: {ex.Message}");
                return new ResultadoOperacion(false,
                    "No se pudo eliminar el evento. Verifica tu conexión.");
            }
        }
    }

    // ══════════════════════════════════════════════════════════
    //  DTOs DE SOPORTE
    // ══════════════════════════════════════════════════════════

    /// <summary>
    /// Objeto de display para el Picker de eventos.
    /// Envuelve un Evento y expone un texto descriptivo formateado.
    ///
    /// EJEMPLO de Descripcion:
    ///   "América vs Chivas  •  Estadio Azteca  (22/04/26 20:00)"
    ///
    /// El Picker usa ToString() para mostrar el texto en pantalla,
    /// y la View accede a .Evento para trabajar con los datos reales.
    /// </summary>
    public class EventoDisplay
    {
        /// <summary>Datos completos del evento (con IDs y precio).</summary>
        public Evento Evento { get; set; } = new();

        /// <summary>
        /// Texto corto para el Picker: "América vs Chivas  •  Estadio Azteca"
        /// Sin fecha, para que el Picker no se vea saturado.
        /// </summary>
        public string Descripcion { get; set; } = string.Empty;

        /// <summary>
        /// Fecha formateada para mostrar FUERA del Picker como Label.
        /// Ej: "miércoles 22/04/2026  •  20:00 hrs"
        /// </summary>
        public string FechaFormateada { get; set; } = string.Empty;

        /// <summary>El Picker usa ToString() como display text.</summary>
        public override string ToString() => Descripcion;
    }

    /// <summary>
    /// Estadísticas calculadas de un evento específico.
    /// </summary>
    public class EstadisticasEvento
    {
        /// <summary>Capacidad total del evento (sin importar ventas).</summary>
        public int     TotalBoletos       { get; set; }
        /// <summary>Boletos que aún se pueden vender.</summary>
        public int     BoletosDisponibles { get; set; }
        /// <summary>Total de boletos vendidos.</summary>
        public int     BoletosVendidos    { get; set; }
        /// <summary>Suma de todas las ventas registradas.</summary>
        public decimal TotalRecaudado     { get; set; }
        /// <summary>Precio unitario por boleto.</summary>
        public decimal PrecioPorBoleto    { get; set; }
    }
}
