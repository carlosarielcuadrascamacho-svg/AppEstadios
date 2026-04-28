// Importa los modelos de datos centrales de la aplicación
using AppEstadios.Models;
// Facilita el acceso a las constantes de filtrado del cliente ORM
using static Postgrest.Constants;

namespace AppEstadios.Controllers
{
    // Esta clase procesa la información espacial y temporal de los partidos programados
    // para generar visualizaciones en mapas interactivos. Actúa como el motor analítico
    // que filtra los eventos futuros, los consolida por recinto deportivo y calcula las
    // métricas financieras clave en tiempo real cuando el usuario interactúa con la interfaz.
    public class ReportesController
    {
        // Este método orquesta la recuperación paralela de eventos, equipos y sedes para
        // construir los puntos de interés que se desplegarán en el mapa. Aplica filtros
        // cronológicos estrictos para asegurar que solo se muestre el partido más cercano
        // en el tiempo para cada estadio, optimizando el rendimiento mediante peticiones concurrentes.
        public async Task<List<PinEventoInfo>> ObtenerPinesEventosFuturosAsync()
        {
            // Abre el bloque de control para interceptar fallos de comunicación con el servidor,
            // evitando que problemas de red provoquen un cierre inesperado de la aplicación.
            try
            {
                // Obtiene el momento exacto actual formateado en el estándar internacional ISO 8601.
                // Esto es necesario para realizar comparaciones precisas contra los registros UTC de la base de datos.
                var ahora = DateTime.UtcNow.ToString("o");

                // Prepara la tarea asíncrona para consultar los eventos que aún no han sucedido.
                // La consulta se configura pero no se bloquea el hilo principal en este punto.
                var tareaEventos  = SupabaseService.Cliente
                    // Especifica la tabla de Evento como origen de los datos
                    .From<Evento>()
                    // Filtra los registros cuya fecha programada sea estrictamente posterior al tiempo actual
                    .Filter("fecha_hora_partido", Operator.GreaterThan, ahora)
                    // Ordena cronológicamente de manera ascendente para tener los juegos más próximos al inicio
                    .Order("fecha_hora_partido", Ordering.Ascending)
                    // Lanza la petición de lectura de datos de forma no bloqueante
                    .Get();

                // Prepara la consulta paralela para traer el catálogo completo de equipos registrados.
                // Esto permitirá resolver los nombres de los clubes a partir de sus identificadores.
                var tareaEquipos  = SupabaseService.Cliente.From<Equipo>().Get();
                
                // Prepara la consulta paralela para traer los datos de localización de estadios.
                // Es indispensable para obtener las coordenadas geográficas necesarias para el mapa.
                var tareaEstadios = SupabaseService.Cliente.From<Estadio>().Get();

                // Ejecuta las tres peticiones de base de datos de forma simultánea.
                // Esta técnica reduce drásticamente el tiempo de espera total al no ejecutar consultas secuenciales.
                await Task.WhenAll(tareaEventos, tareaEquipos, tareaEstadios);

                // Extrae la colección de eventos futuros que fueron devueltos exitosamente por el servidor.
                var eventosFuturos = tareaEventos.Result.Models;
                
                // Indexa los equipos en un diccionario usando su identificador único como llave.
                // Optimiza las búsquedas posteriores evitando iterar sobre la lista repetidamente.
                var dicEquipos     = tareaEquipos.Result.Models
                                        .ToDictionary(e => e.Id, e => e.Nombre);
                                        
                // Indexa los estadios en un diccionario para accesos rápidos mediante su ID único.
                var dicEstadios    = tareaEstadios.Result.Models
                                        .ToDictionary(e => e.Id, e => e);

                // Evalúa si la consulta no arrojó ningún partido programado para el futuro.
                if (eventosFuturos.Count == 0)
                    // Retorna inmediatamente una lista vacía para evitar procesamientos innecesarios.
                    return new List<PinEventoInfo>();

                // Agrupa los partidos por estadio tomando como válido únicamente el más cercano cronológicamente.
                // Esto cumple con la regla de negocio de no saturar el mapa con múltiples eventos por sede.
                var eventosPorEstadio = eventosFuturos
                    // Clasifica el listado completo segmentándolo bajo el criterio del ID del recinto
                    .GroupBy(e => e.EstadioId)
                    // Elige el primer registro de cada segmento, el cual corresponde al partido más próximo
                    .Select(grupo => grupo.First())
                    // Consolida la selección filtrada en una nueva estructura de lista procesable
                    .ToList();

                // Inicializa el contenedor que albergará los datos visuales estructurados finales.
                var pines = new List<PinEventoInfo>();

                // Itera secuencialmente sobre cada uno de los eventos filtrados y seleccionados por recinto.
                foreach (var evento in eventosPorEstadio)
                {
                    // Intenta localizar los datos geográficos y descriptivos del estadio en el diccionario.
                    if (!dicEstadios.TryGetValue(evento.EstadioId, out var estadio))
                        // Omite el registro si la sede no existe o no tiene información asociada.
                        continue;

                    // Verifica que el recinto cuente con coordenadas válidas y no valores por defecto (ceros).
                    if (estadio.Latitud == 0 && estadio.Longitud == 0)
                        // Salta el elemento para prevenir la colocación de marcadores erróneos en el mapa.
                        continue;

                    // Recupera el nombre del club local o establece un valor genérico si no se encuentra en el catálogo.
                    var nombreLocal     = dicEquipos.GetValueOrDefault(evento.LocalId,     "Local");
                    
                    // Recupera el nombre del club visitante o establece un valor genérico de respaldo.
                    var nombreVisitante = dicEquipos.GetValueOrDefault(evento.VisitanteId, "Visitante");
                    
                    // Ajusta la fecha y hora almacenada en formato UTC a la zona horaria local del usuario.
                    var fechaLocal      = evento.FechaHoraPartido.ToLocalTime();

                    // Construye y añade el DTO con toda la información formateada y lista para ser renderizada.
                    pines.Add(new PinEventoInfo
                    {
                        // Vincula el identificador primario del partido en cuestión
                        EventoId        = evento.Id,
                        // Transfiere el nombre legible del recinto deportivo
                        NombreEstadio   = estadio.Nombre,
                        // Guarda el nombre descriptivo del equipo que juega en casa
                        NombreLocal     = nombreLocal,
                        // Guarda el nombre descriptivo del equipo retador
                        NombreVisitante = nombreVisitante,
                        // Genera una cadena amigable con el día de la semana y la fecha completa
                        FechaFormateada = fechaLocal.ToString("dddd dd 'de' MMMM, yyyy"),
                        // Formatea la hora exacta de inicio del partido con un sufijo legible
                        HoraFormateada  = fechaLocal.ToString("HH:mm 'hrs'"),
                        // Asigna la coordenada vertical para la ubicación espacial del marcador
                        Latitud         = estadio.Latitud,
                        // Asigna la coordenada horizontal para la ubicación espacial del marcador
                        Longitud        = estadio.Longitud
                    });
                }

                // Devuelve la colección completa de marcadores geográficos al hilo llamador.
                return pines;
            }
            // Gestiona cualquier tipo de excepción imprevista surgida durante la ejecución del pipeline.
            catch (Exception ex)
            {
                // Registra la traza técnica del error en la salida de diagnóstico del sistema.
                Console.WriteLine($"[ReportesController] Error: {ex.Message}");
                
                // Proporciona una lista vacía segura para evitar fallos en cadena en la interfaz visual.
                return new List<PinEventoInfo>();
            }
        }

        // Esta función realiza una carga diferida de los datos financieros y de asistencia vinculados a un
        // encuentro deportivo específico. Permite obtener el balance de boletaje vendido y la recaudación
        // acumulada únicamente cuando el usuario interactúa directamente con un marcador en el mapa.
        public async Task<EstadisticasEvento> ObtenerEstadisticasAsync(int eventoId)
        {
            // Abre el bloque de captura de excepciones para resguardar la operación de consulta de detalles.
            try
            {
                // Recupera el registro original del partido para extraer su configuración base.
                var respuestaEvento = await SupabaseService.Cliente
                    // Apunta a la tabla de almacenamiento de los eventos deportivos
                    .From<Evento>()
                    // Filtra de manera estricta por el identificador único del partido seleccionado
                    .Filter("id", Operator.Equals, eventoId.ToString())
                    // Restringe la consulta para esperar un único registro coincidente
                    .Single();

                // Valida si la base de datos no logró encontrar información sobre el evento solicitado.
                if (respuestaEvento == null) return new EstadisticasEvento();

                // Almacena la entidad recuperada para procesar sus propiedades financieras y operativas.
                var evento = respuestaEvento;

                // Realiza la consulta de las transacciones comerciales ligadas al partido.
                var respuestaVentas = await SupabaseService.Cliente
                    // Accede a la colección de datos donde se registran las compras efectuadas
                    .From<Venta>()
                    // Filtra únicamente los recibos que pertenezcan al evento bajo análisis
                    .Filter("evento_id", Operator.Equals, eventoId.ToString())
                    // Ejecuta la operación de extracción de datos de ventas
                    .Get();

                // Guarda la lista de compras concretadas para su posterior análisis numérico.
                var ventas = respuestaVentas.Models;
                
                // Acumula la cantidad total de accesos que han sido adquiridos por los usuarios.
                int totalVendidos = ventas.Sum(v => v.CantidadBoletos);
                
                // Calcula el monto económico global recaudado por la venta de entradas del juego.
                decimal totalRecaudado = ventas.Sum(v => v.TotalCobrado);

                // Retorna el modelo consolidado de métricas financieras e inventario de boletaje.
                return new EstadisticasEvento
                {
                    // Establece la capacidad máxima de asistentes permitida en el evento
                    TotalBoletos = evento.TotalBoletos,
                    // Deduce la cantidad de asientos aún disponibles restando las compras realizadas
                    BoletosDisponibles = evento.TotalBoletos - totalVendidos,
                    // Asigna el total acumulado de entradas efectivamente comercializadas
                    BoletosVendidos = totalVendidos,
                    // Registra la cifra monetaria bruta generada hasta el momento
                    TotalRecaudado = totalRecaudado,
                    // Fija el costo unitario establecido para la compra de un boleto
                    PrecioPorBoleto = evento.PrecioBoleto
                };
            }
            // Administra cualquier interrupción técnica ocurrida en el proceso de cálculo diferido.
            catch (Exception ex)
            {
                // Envía el reporte detallado del error a la consola de administración
                Console.WriteLine($"[ReportesController] Error al obtener estadísticas: {ex.Message}");
                
                // Responde con una estructura por defecto para mantener la estabilidad del sistema.
                return new EstadisticasEvento();
            }
        }

        // Este algoritmo geométrico determina el encuadre visual óptimo del mapa analizando la dispersión
        // geográfica de todos los puntos activos. Calcula las coordenadas centrales y deduce el nivel de
        // acercamiento adecuado para garantizar la visibilidad simultánea de todos los marcadores.
        public static RegionMapa CalcularRegion(List<PinEventoInfo> pines)
        {
            // Determina el comportamiento en caso de que no existan marcadores disponibles para evaluar.
            if (pines.Count == 0)
                // Establece un punto de vista neutral centrado en la capital del país como respaldo lógico.
                return new RegionMapa { Latitud = 19.4326, Longitud = -99.1332, ZoomLevel = 5 };

            // Modifica la estrategia visual si únicamente se debe proyectar un solo recinto deportivo.
            if (pines.Count == 1)
                // Centra la cámara directamente sobre el único estadio proporcionado.
                return new RegionMapa
                {
                    // Fija la coordenada vertical del marcador como punto focal
                    Latitud   = pines[0].Latitud,
                    // Fija la coordenada horizontal del marcador como punto focal
                    Longitud  = pines[0].Longitud,
                    // Aplica un nivel de zoom cerrado de escala urbana
                    ZoomLevel = 12
                };

            // Identifica los extremos geográficos norte, sur, este y oeste de la distribución de puntos.
            double latMin = pines.Min(p => p.Latitud);
            double latMax = pines.Max(p => p.Latitud);
            double lonMin = pines.Min(p => p.Longitud);
            double lonMax = pines.Max(p => p.Longitud);

            // Calcula el promedio aritmético para posicionar el centro geométrico exacto del encuadre.
            double latCenter = (latMin + latMax) / 2;
            double lonCenter = (lonMin + lonMax) / 2;

            // Mide la amplitud angular más significativa entre ambos ejes espaciales.
            double maxSpan   = Math.Max(latMax - latMin, lonMax - lonMin);
            
            // Deriva matemáticamente el zoom óptimo adaptando el espacio visible a las dimensiones de la pantalla.
            double zoomLevel = Math.Max(2, Math.Min(14,
                               Math.Log(360.0 / (maxSpan * 1.4)) / Math.Log(2)));

            // Entrega la configuración de cámara computada para alimentar el control del mapa.
            return new RegionMapa
            {
                // Asigna la coordenada de latitud del punto central
                Latitud   = latCenter,
                // Asigna la coordenada de longitud del punto central
                Longitud  = lonCenter,
                // Asigna el grado de acercamiento calculado para el marco visual
                ZoomLevel = zoomLevel
            };
        }
    }

    // Esta estructura ligera de datos sirve para desacoplar la lógica de negocio de los componentes visuales del mapa.
    // Permite transportar la información geográfica y descriptiva esencial sin dependencias directas.
    public class PinEventoInfo
    {
        // Llave primaria única del evento deportivo representado
        public int    EventoId        { get; set; }
        // Nombre descriptivo completo de la sede del encuentro
        public string NombreEstadio   { get; set; } = string.Empty;
        // Nombre comercial del club anfitrión
        public string NombreLocal     { get; set; } = string.Empty;
        // Nombre comercial del club visitante
        public string NombreVisitante { get; set; } = string.Empty;
        // Formato textual amigable para la visualización de la fecha del partido
        public string FechaFormateada { get; set; } = string.Empty;
        // Formato textual amigable para la visualización de la hora de inicio
        public string HoraFormateada  { get; set; } = string.Empty;
        // Posición geográfica vertical del recinto
        public double Latitud         { get; set; }
        // Posición geográfica horizontal del recinto
        public double Longitud        { get; set; }
    }

    // Esta estructura complementaria define la posición espacial y escala visual del mapa.
    public class RegionMapa
    {
        // Punto de enfoque vertical para centrar el mapa
        public double Latitud   { get; set; }
        // Punto de enfoque horizontal para centrar el mapa
        public double Longitud  { get; set; }
        // Representación numérica de la escala de visualización activa
        public double ZoomLevel { get; set; }
    }
}
