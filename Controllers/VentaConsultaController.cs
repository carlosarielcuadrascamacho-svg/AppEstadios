// Traemos los modelos de datos básicos que representan cosas como Eventos o Ventas
using AppEstadios.Models;
// Conseguimos herramientas para ordenar la información que viene de la base de datos
using static Postgrest.Constants;

namespace AppEstadios.Controllers
{
    // Esta clase se encarga de todo el proceso para vender entradas de los partidos de fútbol.
    // Funciona como el cerebro que junta la información de los equipos y los estadios para mostrarla
    // de forma bonita, lleva la cuenta de cuánto dinero se va ganando y cuántos asientos quedan
    // libres, y se asegura de que nadie compre boletos de más o use datos incorrectos.
    public class VentaConsultaController
    {
        // Esta función se encarga de buscar todos los partidos que están organizados y les pega la
        // información real de qué equipos juegan y en qué lugar lo harán. Prepara estos datos con un
        // texto fácil de leer para que el usuario pueda elegir cómodamente qué partido quiere gestionar.
        public async Task<List<EventoDisplay>> ObtenerEventosConDetalleAsync()
        {
            // Usamos un bloque de protección por si falla el internet al pedir los datos
            try
            {
                // Pedimos la lista de partidos ordenados por fecha, del más viejo al más nuevo
                var tareaEventos   = SupabaseService.Cliente.From<Evento>()
                                        .Order("fecha_hora_partido", Ordering.Ascending)
                                        .Get();
                                        
                // Pedimos la lista completa de todos los equipos de fútbol registrados
                var tareaEquipos   = SupabaseService.Cliente.From<Equipo>().Get();
                
                // Pedimos la lista de todos los recintos deportivos disponibles
                var tareaEstadios  = SupabaseService.Cliente.From<Estadio>().Get();

                // Esperamos pacientemente a que las tres peticiones terminen al mismo tiempo
                await Task.WhenAll(tareaEventos, tareaEquipos, tareaEstadios);

                // Guardamos los equipos en una lista rápida usando su número de identificación
                var dicEquipos  = tareaEquipos.Result.Models
                                    .ToDictionary(e => e.Id, e => e.Nombre);
                                    
                // Guardamos los estadios de la misma forma para encontrarlos en un abrir y cerrar de ojos
                var dicEstadios = tareaEstadios.Result.Models
                                    .ToDictionary(e => e.Id, e => e.Nombre);

                // Creamos una lista vacía donde guardaremos los partidos ya armados y listos
                var resultado = new List<EventoDisplay>();

                // Revisamos uno por uno cada partido que encontramos en el servidor
                foreach (var evento in tareaEventos.Result.Models)
                {
                    // Buscamos el nombre del equipo local o ponemos un texto de duda si no aparece
                    var nombreLocal      = dicEquipos.GetValueOrDefault(evento.LocalId,     "Equipo ?");
                    
                    // Buscamos el nombre del equipo visitante usando su número de identificación
                    var nombreVisitante  = dicEquipos.GetValueOrDefault(evento.VisitanteId, "Equipo ?");
                    
                    // Buscamos el lugar físico donde se jugará el partido
                    var nombreEstadio    = dicEstadios.GetValueOrDefault(evento.EstadioId,  "Estadio ?");

                    // Convertimos el horario del servidor al horario real de nuestro teléfono
                    var fechaFormateada = evento.FechaHoraPartido.ToLocalTime()
                                                .ToString("dd/MM/yy HH:mm");

                    // Mejora #6: Prefijo emoji según el estado del evento para lectura instantánea
                    var ahora        = DateTime.UtcNow;
                    var fechaEvento  = evento.FechaHoraPartido;
                    var diasRestantes = (fechaEvento - ahora).TotalDays;

                    string prefijo = diasRestantes < 0
                        ? "✅ "  // Ya pasó el partido
                        : diasRestantes <= 3
                            ? "🔒 " // En período de corte (≤3 días)
                            : "";  // Venta abierta: sin prefijo

                    // Agregamos el partido ya preparado a nuestra lista de resultados
                    resultado.Add(new EventoDisplay
                    {
                        // Guardamos toda la información original del partido por si la necesitamos
                        Evento         = evento,
                        // Creamos un texto amigable con prefijo de estado para identificación rápida
                        Descripcion    = $"{prefijo}{nombreLocal} vs {nombreVisitante}  •  {nombreEstadio}",
                        // Guardamos la fecha escrita de forma larga y muy legible para las pantallas
                        FechaFormateada= evento.FechaHoraPartido.ToLocalTime()
                                                .ToString("dddd dd/MM/yyyy  \u2022  HH:mm \"hrs\"")
                    });

                }

                // Entregamos la lista completa con todos los partidos bien detallados
                return resultado;
            }
            // Si algo sale mal con la conexión, atrapamos el error aquí
            catch (Exception ex)
            {
                // Imprimimos un mensaje de alerta en la computadora para saber qué falló
                Console.WriteLine($"[VentaConsultaController] Error al cargar eventos: {ex.Message}");
                
                // Entregamos una lista vacía para que la aplicación no se trabe ni se cierre
                return new List<EventoDisplay>();
            }
        }

        // Este apartado hace las cuentas matemáticas necesarias para saber cómo va la venta de un partido
        // en específico. Suma todas las compras que se han hecho hasta el momento para calcular rápidamente
        // cuánto dinero ha entrado a la caja y cuántas entradas quedan todavía disponibles para la gente.
        public async Task<EstadisticasEvento> ObtenerEstadisticasAsync(Evento evento)
        {
            // Usamos un bloque de seguridad para controlar fallos inesperados de lectura
            try
            {
                // Buscamos en la base de datos todas las compras que se han hecho para este partido
                var respuestaVentas = await SupabaseService.Cliente
                    .From<Venta>()
                    .Filter("evento_id", Operator.Equals, evento.Id.ToString())
                    .Get();

                // Guardamos las compras encontradas en una lista de trabajo
                var ventas = respuestaVentas.Models;

                // Sumamos todos los boletos que la gente ha comprado hasta el día de hoy
                int     totalVendidos  = ventas.Sum(v => v.CantidadBoletos);
                
                // Sumamos el dinero total que hemos cobrado por todas esas compras
                decimal totalRecaudado = ventas.Sum(v => v.TotalCobrado);
                
                // Restamos los boletos vendidos del total permitido para saber cuántos quedan
                int     disponibles    = evento.TotalBoletos - totalVendidos;

                // Construimos el resumen final con todos los números ya calculados
                return new EstadisticasEvento
                {
                    // Anotamos el cupo máximo del partido
                    TotalBoletos       = evento.TotalBoletos,
                    // Anotamos los lugares libres que quedan
                    BoletosDisponibles = disponibles,
                    // Anotamos las ganancias acumuladas
                    TotalRecaudado     = totalRecaudado,
                    // Anotamos cuánto cuesta cada boleto individual
                    PrecioPorBoleto    = evento.PrecioBoleto
                };
            }
            // Si ocurre algún problema al hacer las operaciones o consultar datos
            catch (Exception ex)
            {
                // Mostramos el mensaje del problema en el registro interno de la aplicación
                Console.WriteLine($"[VentaConsultaController] Error al calcular estadísticas: {ex.Message}");
                
                // Respondemos con los datos básicos del partido pero con las ventas en ceros por seguridad
                return new EstadisticasEvento
                {
                    TotalBoletos       = evento.TotalBoletos,
                    BoletosDisponibles = 0,
                    TotalRecaudado     = 0,
                    PrecioPorBoleto    = evento.PrecioBoleto
                };
            }
        }

        // Esta función procesa la compra de un cliente asegurándose de que todo esté en orden antes de
        // guardar el dinero. Revisa con cuidado que el comprador haya dejado su nombre, que su número
        // de teléfono tenga los dígitos exactos y que todavía queden suficientes asientos libres.
        public async Task<ResultadoOperacion> GuardarVentaAsync(
            Evento? evento,
            string nombreCliente,
            string telefono,
            string cantBoletosTexto,
            EstadisticasEvento? estadisticas)
        {
            // Verificamos que el usuario haya seleccionado un partido antes de continuar
            if (evento is null)
                return new ResultadoOperacion(false, "Selecciona un evento primero.");

            // Comprobamos que no hayan dejado el espacio del nombre del cliente en blanco
            if (string.IsNullOrWhiteSpace(nombreCliente))
                return new ResultadoOperacion(false, "El nombre del cliente es obligatorio.");

            // El nombre debe tener al menos 3 caracteres
            if (nombreCliente.Trim().Length < 3)
                return new ResultadoOperacion(false, "El nombre del cliente debe tener al menos 3 caracteres.");

            // El nombre debe contener al menos una letra (no puede ser solo números o símbolos)
            if (!nombreCliente.Any(char.IsLetter))
                return new ResultadoOperacion(false, "El nombre del cliente debe contener letras, no solo números o símbolos.");


            // Nos aseguramos de que hayan escrito un número de teléfono para contacto
            if (string.IsNullOrWhiteSpace(telefono))
                return new ResultadoOperacion(false, "El teléfono es obligatorio.");

            // Limpiamos el texto del teléfono para quedarnos exclusivamente con los números puros
            var telefonoLimpio = new string(telefono.Where(char.IsDigit).ToArray());
            
            // Validamos que el número tenga exactamente los 10 dígitos requeridos en el país
            if (telefonoLimpio.Length != 10)
                return new ResultadoOperacion(false, "El teléfono debe contener exactamente 10 dígitos numéricos.");

            // Intentamos convertir el texto de cantidad de boletos a un número real y positivo
            if (!int.TryParse(cantBoletosTexto, out int cantidad) || cantidad <= 0)
                return new ResultadoOperacion(false, "La cantidad de boletos debe ser mayor a cero.");

            // Averiguamos cuántos boletos quedan libres en el estadio en este momento
            int disponibles = estadisticas?.BoletosDisponibles ?? 0;
            
            // Si el cliente pide más boletos de los que existen, detenemos la venta cortésmente
            if (cantidad > disponibles)
                return new ResultadoOperacion(false,
                    $"No hay suficientes boletos. Disponibles: {disponibles}.");

            // Calculamos el precio total multiplicando la cantidad por el costo de cada entrada
            decimal totalCobrado = cantidad * evento.PrecioBoleto;

            // Armamos el recibo de compra con toda la información del cliente y del partido
            var nuevaVenta = new Venta
            {
                // Asociamos la compra con el partido correcto
                EventoId        = evento.Id,
                // Guardamos el nombre del comprador quitando espacios sobrantes a los lados
                NombreCliente   = nombreCliente.Trim(),
                // Guardamos el número telefónico limpio de 10 dígitos
                Telefono        = new string(telefono.Where(char.IsDigit).ToArray()),
                // Anotamos cuántos boletos se está llevando
                CantidadBoletos = cantidad,
                // Anotamos el dinero total que nos debe pagar
                TotalCobrado    = totalCobrado,
                // Registramos el momento exacto en el que se hizo la transacción
                FechaVenta      = DateTime.UtcNow
            };

            // Intentamos guardar el nuevo recibo de compra en nuestra base de datos
            try
            {
                // Enviamos la información de la venta al servidor en internet
                await SupabaseService.Cliente.From<Venta>().Insert(nuevaVenta);

                // Si todo salió bien, devolvemos una confirmación bonita con los datos clave
                return new ResultadoOperacion(
                    true,
                    $"✅ Venta registrada exitosamente.\n" +
                    $"Cliente: {nuevaVenta.NombreCliente}\n" +
                    $"Boletos: {cantidad}   Total: ${totalCobrado:F2}");
            }
            // Si falla la conexión o el servidor rechaza la compra
            catch (Exception ex)
            {
                // Anotamos el fallo en el historial interno de la aplicación
                Console.WriteLine($"[VentaConsultaController] Error al guardar venta: {ex.Message}");
                
                // Avisamos al usuario que hubo un problema para que lo intente más tarde
                return new ResultadoOperacion(false,
                    "Error al registrar la venta. Verifica tu conexión e intenta de nuevo.");
            }
        }

        // Este valor representa cuántos días antes del partido se debe cerrar la venta al público.
        // Si faltan 3 días o menos para el juego, ya no se permitirá comprar más entradas.
        private const int DiasCorteVenta = 3;

        // Este control de seguridad decide si un partido todavía acepta que se le vendan boletos o si ya
        // es demasiado tarde. Aplica reglas lógicas como cerrar la venta unos días antes del juego para
        // evitar problemas, avisar si el partido ya terminó, o notificar si ya no hay lugares.
        public (bool Disponible, string? Motivo) VerificarDisponibilidadVenta(
            Evento evento,
            EstadisticasEvento estadisticas)
        {
            // Obtenemos el momento exacto del día de hoy
            var ahora = DateTime.UtcNow;
            
            // Traemos la fecha y hora en la que se jugará el partido
            var fechaEvento = evento.FechaHoraPartido;

            // Verificamos si el partido ya se jugó o está ocurriendo justo ahora
            if (fechaEvento <= ahora)
                // Bloqueamos la venta avisando que el juego ya concluyó
                return (false, "❌ Evento finalizado. Solo consulta disponible.");

            // Calculamos cuántos días faltan exactamente para que empiece el juego
            var diasRestantes = (fechaEvento - ahora).TotalDays;
            
            // Comprobamos si estamos dentro del periodo de tiempo prohibido para vender
            if (diasRestantes <= DiasCorteVenta)
                // Informamos al vendedor que el plazo límite de compra ha caducado
                return (false, $"🔒 Venta cerrada. El partido es en {(int)Math.Ceiling(diasRestantes)} día(s). " +
                               $"La venta cierra {DiasCorteVenta} días antes del evento.");

            // Revisamos si ya nos quedamos sin boletos que ofrecer
            if (estadisticas.BoletosDisponibles <= 0)
                // Avisamos que se han agotado absolutamente todas las entradas
                return (false, "🎫 Agotado. Todos los boletos han sido vendidos.");

            // Si no se rompió ninguna regla, damos luz verde para que proceda la venta
            return (true, null);
        }

        // Esta herramienta permite dar de baja un partido completo del sistema si ya no se va a llevar a cabo.
        // Al borrar el evento, el sistema automáticamente limpia los registros y borra todas las ventas asociadas.
        public async Task<ResultadoOperacion> EliminarEventoAsync(Evento? evento)
        {
            // Nos aseguramos de que el usuario haya elegido de verdad un partido para borrar
            if (evento is null)
                return new ResultadoOperacion(false, "No hay evento seleccionado para eliminar.");

            // Intentamos realizar la eliminación de forma segura en el servidor
            try
            {
                // Le pedimos a la base de datos que busque el partido por su ID y lo borre
                await SupabaseService.Cliente
                    .From<Evento>()
                    .Filter("id", Operator.Equals, evento.Id.ToString())
                    .Delete();

                // Devolvemos una confirmación alegre de que el juego fue eliminado
                return new ResultadoOperacion(true, "Evento eliminado correctamente.");
            }
            // En caso de que la base de datos no responda o rechace la orden
            catch (Exception ex)
            {
                // Dejamos registro del fallo en la consola para revisión técnica
                Console.WriteLine($"[VentaConsultaController] Error al eliminar evento: {ex.Message}");
                
                // Avisamos amigablemente al usuario que no pudimos completar su solicitud
                return new ResultadoOperacion(false,
                    "No se pudo eliminar el evento. Verifica tu conexión.");
            }
        }
    }

    // Estos pequeños paquetes de información sirven para ordenar los datos de forma que sean muy
    // fáciles de entender por las pantallas del teléfono. Ayudan a guardar resúmenes legibles.
    public class EventoDisplay
    {
        // Aquí guardamos la información completa y original que viene desde el servidor
        public Evento Evento { get; set; } = new();

        // Este es el texto corto que se verá en las listas, como "Equipo A vs Equipo B en Estadio X"
        public string Descripcion { get; set; } = string.Empty;

        // Aquí guardamos la fecha bien escrita y bonita para mostrarla fuera de las listas
        public string FechaFormateada { get; set; } = string.Empty;

        // Le decimos al sistema que cuando intente leer este paquete, muestre directamente su descripción
        public override string ToString() => Descripcion;
    }

    // Este paquete guarda exclusivamente los cálculos matemáticos de asistencia y dinero del partido
    public class EstadisticasEvento
    {
        // La cantidad máxima de personas que caben en el partido
        public int     TotalBoletos       { get; set; }
        // El número de asientos que todavía nadie ha comprado
        public int     BoletosDisponibles { get; set; }
        // La cantidad total de boletos que ya logramos colocar
        public int     BoletosVendidos    { get; set; }
        // El acumulado de dinero que hemos cobrado en total
        public decimal TotalRecaudado     { get; set; }
        // Cuánto cuesta una sola entrada al evento deportivo
        public decimal PrecioPorBoleto    { get; set; }
    }
}
