using AppEstadios.Models;
using static Postgrest.Constants;

namespace AppEstadios.Controllers
{
    /// <summary>
    /// ReportesController — Controlador del mapa de eventos próximos.
    ///
    /// REGLA FUNDAMENTAL DE NEGOCIO:
    ///   Solo eventos FUTUROS y solo el MÁS PRÓXIMO por estadio.
    ///
    /// FLUJO:
    ///   1. Filtrar: eventos con FechaHoraPartido > DateTime.UtcNow
    ///   2. Agrupar: por EstadioId
    ///   3. Seleccionar mínimo: el primero cronológicamente por grupo
    ///   4. Proyectar: a PinEventoInfo con lat/lon y datos de display
    ///
    /// NOTA: Este controlador es agnóstico al proveedor de mapas (Syncfusion / MAUI Maps / etc).
    ///   Solo retorna DTOs con datos. La View crea los marcadores específicos de la librería.
    /// </summary>
    public class ReportesController
    {
        // ══════════════════════════════════════════════════════════
        //  MÉTODO PRINCIPAL — Pipeline de datos para el mapa
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Ejecuta el pipeline completo:
        ///   Filtrar futuros → Agrupar por estadio → Seleccionar mínimo → Proyectar a DTO.
        ///
        /// RETORNA: Lista de PinEventoInfo lista para que la View cree los marcadores.
        /// </summary>
        public async Task<List<PinEventoInfo>> ObtenerPinesEventosFuturosAsync()
        {
            try
            {
                // ── Paso 1: 3 consultas en paralelo ──
                var ahora = DateTime.UtcNow.ToString("o"); // ISO 8601

                var tareaEventos  = SupabaseService.Cliente
                    .From<Evento>()
                    .Filter("fecha_hora_partido", Operator.GreaterThan, ahora)
                    .Order("fecha_hora_partido", Ordering.Ascending)
                    .Get();

                var tareaEquipos  = SupabaseService.Cliente.From<Equipo>().Get();
                var tareaEstadios = SupabaseService.Cliente.From<Estadio>().Get();

                await Task.WhenAll(tareaEventos, tareaEquipos, tareaEstadios);

                var eventosFuturos = tareaEventos.Result.Models;
                var dicEquipos     = tareaEquipos.Result.Models
                                        .ToDictionary(e => e.Id, e => e.Nombre);
                var dicEstadios    = tareaEstadios.Result.Models
                                        .ToDictionary(e => e.Id, e => e);

                if (eventosFuturos.Count == 0)
                    return new List<PinEventoInfo>();

                // ── Paso 2: Agrupar por estadio → tomar el MÁS PRÓXIMO ──
                // La lista ya viene en ASC por fecha, el primero de cada grupo = más próximo
                var eventosPorEstadio = eventosFuturos
                    .GroupBy(e => e.EstadioId)
                    .Select(grupo => grupo.First())
                    .ToList();

                // ── Paso 3: Proyectar a PinEventoInfo ──
                var pines = new List<PinEventoInfo>();

                foreach (var evento in eventosPorEstadio)
                {
                    if (!dicEstadios.TryGetValue(evento.EstadioId, out var estadio))
                        continue;

                    // Saltamos estadios sin coordenadas configuradas
                    if (estadio.Latitud == 0 && estadio.Longitud == 0)
                        continue;

                    var nombreLocal     = dicEquipos.GetValueOrDefault(evento.LocalId,     "Local");
                    var nombreVisitante = dicEquipos.GetValueOrDefault(evento.VisitanteId, "Visitante");
                    var fechaLocal      = evento.FechaHoraPartido.ToLocalTime();

                    pines.Add(new PinEventoInfo
                    {
                        EventoId        = evento.Id,
                        NombreEstadio   = estadio.Nombre,
                        NombreLocal     = nombreLocal,
                        NombreVisitante = nombreVisitante,
                        FechaFormateada = fechaLocal.ToString("dddd dd 'de' MMMM, yyyy"),
                        HoraFormateada  = fechaLocal.ToString("HH:mm 'hrs'"),
                        Latitud         = estadio.Latitud,
                        Longitud        = estadio.Longitud
                    });
                }

                return pines;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ReportesController] Error: {ex.Message}");
                return new List<PinEventoInfo>();
            }
        }

        // ══════════════════════════════════════════════════════════
        //  ESTADÍSTICAS DEL EVENTO (LAZY LOAD)
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Obtiene de forma perezosa (lazy) los boletos vendidos y total recaudado
        /// para un evento específico al tocar su pin.
        /// </summary>
        public async Task<EstadisticasEvento> ObtenerEstadisticasAsync(int eventoId)
        {
            try
            {
                // Obtenemos el evento para conocer el total de boletos y precio
                var respuestaEvento = await SupabaseService.Cliente
                    .From<Evento>()
                    .Filter("id", Operator.Equals, eventoId.ToString())
                    .Single();

                if (respuestaEvento == null) return new EstadisticasEvento();

                var evento = respuestaEvento;

                // Obtenemos las ventas
                var respuestaVentas = await SupabaseService.Cliente
                    .From<Venta>()
                    .Filter("evento_id", Operator.Equals, eventoId.ToString())
                    .Get();

                var ventas = respuestaVentas.Models;
                int totalVendidos = ventas.Sum(v => v.CantidadBoletos);
                decimal totalRecaudado = ventas.Sum(v => v.TotalCobrado);

                return new EstadisticasEvento
                {
                    TotalBoletos = evento.TotalBoletos,
                    BoletosDisponibles = evento.TotalBoletos - totalVendidos,
                    BoletosVendidos = totalVendidos,
                    TotalRecaudado = totalRecaudado,
                    PrecioPorBoleto = evento.PrecioBoleto
                };
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ReportesController] Error al obtener estadísticas: {ex.Message}");
                return new EstadisticasEvento();
            }
        }

        // ══════════════════════════════════════════════════════════
        //  HELPER — Calcular región central del mapa
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Calcula el centro y el zoom level óptimo para mostrar todos los pines.
        /// Retorna un RegionMapa agnóstico a la librería de mapas.
        /// </summary>
        public static RegionMapa CalcularRegion(List<PinEventoInfo> pines)
        {
            // Sin pines → CDMX como fallback
            if (pines.Count == 0)
                return new RegionMapa { Latitud = 19.4326, Longitud = -99.1332, ZoomLevel = 5 };

            // Un solo pin → zoom de ciudad
            if (pines.Count == 1)
                return new RegionMapa
                {
                    Latitud   = pines[0].Latitud,
                    Longitud  = pines[0].Longitud,
                    ZoomLevel = 12
                };

            // Múltiples pines → bounding box con padding
            double latMin = pines.Min(p => p.Latitud);
            double latMax = pines.Max(p => p.Latitud);
            double lonMin = pines.Min(p => p.Longitud);
            double lonMax = pines.Max(p => p.Longitud);

            double latCenter = (latMin + latMax) / 2;
            double lonCenter = (lonMin + lonMax) / 2;

            // Convertimos el span geográfico a zoom level aproximado
            double maxSpan   = Math.Max(latMax - latMin, lonMax - lonMin);
            double zoomLevel = Math.Max(2, Math.Min(14,
                               Math.Log(360.0 / (maxSpan * 1.4)) / Math.Log(2)));

            return new RegionMapa
            {
                Latitud   = latCenter,
                Longitud  = lonCenter,
                ZoomLevel = zoomLevel
            };
        }
    }

    // ══════════════════════════════════════════════════════════
    //  DTOs
    // ══════════════════════════════════════════════════════════

    /// <summary>
    /// DTO con los datos de un marcador de evento.
    /// Agnóstico a la librería de mapas — solo datos puros.
    /// La View crea el marcador específico (MapMarker de Syncfusion, etc.).
    /// </summary>
    public class PinEventoInfo
    {
        public int    EventoId        { get; set; }
        public string NombreEstadio   { get; set; } = string.Empty;
        public string NombreLocal     { get; set; } = string.Empty;
        public string NombreVisitante { get; set; } = string.Empty;
        public string FechaFormateada { get; set; } = string.Empty;
        public string HoraFormateada  { get; set; } = string.Empty;
        public double Latitud         { get; set; }
        public double Longitud        { get; set; }
    }

    /// <summary>
    /// DTO de región del mapa: centro + zoom level.
    /// Compatible con cualquier librería de mapas.
    /// </summary>
    public class RegionMapa
    {
        public double Latitud   { get; set; }
        public double Longitud  { get; set; }
        /// <summary>1 = mundo, 5 = país, 10 = ciudad, 15 = calle</summary>
        public double ZoomLevel { get; set; }
    }
}
