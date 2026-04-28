// Importa las librerías para el manejo de formatos de fecha y cultura del sistema
using System.Globalization;
// Importa los modelos de datos de la aplicación para gestionar entidades como Equipo y Estadio
using AppEstadios.Models;
// Facilita el uso de constantes de ordenamiento para las consultas de Postgrest
using static Postgrest.Constants;

// Define el espacio de nombres para los controladores encargados del flujo de eventos
namespace AppEstadios.Controllers
{
    // Esta clase gestiona el flujo de registro de nuevos eventos deportivos en el sistema. Actúa como el orquestador entre la interfaz de usuario y el almacenamiento persistente, encargándose de recuperar los catálogos de equipos y estadios necesarios para el formulario, aplicar reglas de negocio estrictas sobre los datos ingresados y prevenir duplicidades antes de consolidar la información.
    public class RegistroEventoController
    {
        // Este método se encarga de consultar y retornar el catálogo completo de equipos de fútbol disponibles en la base de datos. Su propósito es proveer los datos necesarios para poblar las opciones de selección en la interfaz de usuario, asegurando una experiencia fluida al ordenar los resultados alfabéticamente.
        public async Task<List<Equipo>> ObtenerEquiposAsync()
        {
            // Abre un bloque de captura para prevenir cierres inesperados por fallos de red
            try
            {
                // Consulta la tabla de equipos a través del cliente de Supabase de manera asíncrona
                var respuesta = await SupabaseService.Cliente
                    // Especifica que la consulta se realizará sobre la entidad de tipo Equipo
                    .From<Equipo>()
                    // Ordena los resultados alfabéticamente por el campo nombre de forma ascendente
                    .Order("nombre", Ordering.Ascending)
                    // Ejecuta la petición de lectura en la base de datos
                    .Get();

                // Retorna la lista de objetos de tipo Equipo extraída de la respuesta del servidor
                return respuesta.Models;
            }
            // Captura cualquier excepción producida durante la consulta externa
            catch (Exception ex)
            {
                // Registra los detalles del error en la consola para facilitar el diagnóstico del desarrollador
                Console.WriteLine($"[RegistroEventoController] Error al cargar equipos: {ex.Message}");

                // Retorna una colección vacía para evitar errores de referencia nula en la vista
                return new List<Equipo>();
            }
        }

        // Este método extrae la lista de recintos deportivos registrados en el sistema para su uso en los controles de selección de la vista. Garantiza que el usuario pueda elegir una sede válida para el evento, gestionando posibles fallos de conexión para no interrumpir la navegación de la aplicación.
        public async Task<List<Estadio>> ObtenerEstadiosAsync()
        {
            // Abre un bloque try para interceptar posibles excepciones en la petición
            try
            {
                // Realiza la solicitud de lectura de estadios al servicio en la nube
                var respuesta = await SupabaseService.Cliente
                    // Indica que la consulta mapeará los datos a la estructura de la clase Estadio
                    .From<Estadio>()
                    // Aplica un ordenamiento ascendente basado en la columna del nombre del estadio
                    .Order("nombre", Ordering.Ascending)
                    // Despacha la consulta de selección de registros
                    .Get();

                // Devuelve la lista limpia de estadios procesados por el ORM
                return respuesta.Models;
            }
            // Gestiona errores de comunicación o configuración del cliente de base de datos
            catch (Exception ex)
            {
                // Imprime la advertencia del fallo en los logs de ejecución de la aplicación
                Console.WriteLine($"[RegistroEventoController] Error al cargar estadios: {ex.Message}");
                // Proporciona una lista sin elementos como respaldo seguro para el control Picker
                return new List<Estadio>();
            }
        }

        // Esta función interna actúa como el primer filtro de seguridad de los datos del formulario antes de interactuar con servicios externos. Implementa un patrón de salida temprana para verificar que no existan omisiones, incongruencias lógicas como enfrentar a un equipo contra sí mismo, o valores numéricos inválidos en la capacidad y costos.
        private string? ValidarCampos(
            Equipo? equipoLocal,
            Equipo? equipoVisitante,
            Estadio? estadio,
            string totalBoletosTexto,
            string precioTexto,
            DateTime fecha,
            TimeSpan hora)
        {
            // Evalúa si se omitió la selección del primer participante del encuentro
            if (equipoLocal is null)
                // Devuelve una alerta descriptiva indicando la falta del equipo local
                return "Debes seleccionar el equipo local.";

            // Verifica la presencia del segundo participante para conformar el partido
            if (equipoVisitante is null)
                // Devuelve el texto de error correspondiente a la ausencia del visitante
                return "Debes seleccionar el equipo visitante.";

            // Compara los identificadores únicos para asegurar que los contendientes sean distintos
            if (equipoLocal.Id == equipoVisitante.Id)
                // Restringe la lógica deportiva impidiendo duelos de un club contra sí mismo
                return "El equipo local y visitante no pueden ser el mismo.";

            // Confirma que se haya establecido el lugar físico donde se llevará a cabo el juego
            if (estadio is null)
                // Informa al usuario la necesidad de asociar un recinto al evento
                return "Debes seleccionar un estadio.";

            // Intenta convertir el texto de boletos a entero y valida que represente una cantidad positiva
            if (!int.TryParse(totalBoletosTexto, out int totalBoletos) || totalBoletos <= 0)
                // Rechaza valores nulos, negativos o cadenas de texto no numéricas
                return "El total de boletos debe ser un número mayor a cero.";

            // Verifica la conversión del precio a formato decimal aplicando reglas independientes de cultura regional
            if (!decimal.TryParse(precioTexto, NumberStyles.Any, CultureInfo.InvariantCulture, out decimal precio) &&
                // Intenta el parseo alternativo utilizando la configuración regional activa del dispositivo
                !decimal.TryParse(precioTexto, NumberStyles.Any, CultureInfo.CurrentCulture, out precio) ||
                // Comprueba que el valor monetario resultante no sea menor o igual a cero
                precio <= 0)
                // Reporta el formato incorrecto o la inviabilidad económica del costo del boleto
                return "El precio del boleto debe ser un número mayor a cero (ej. 150 o 150.50).";

            // Agrega el componente de tiempo del TimePicker a la fecha base seleccionada
            var fechaHoraCompleta = fecha.Date + hora;
            // Compara el momento del partido contra la hora actual del sistema
            if (fechaHoraCompleta <= DateTime.Now)
                // Evita la creación de eventos deportivos programados en el pasado
                return "La fecha y hora del partido debe ser en el futuro.";

            // Indica mediante un valor nulo que la validación de campos fue completada con éxito
            return null;
        }

        // Este método coordina todo el proceso de persistencia de un nuevo partido de fútbol tras validar satisfactoriamente los datos de entrada. Realiza conversiones seguras de tipos de datos, verifica en la base de datos que no se intente registrar un juego idéntico en la misma fecha y ubicación, y finalmente consolida el registro en el servidor.
        public async Task<ResultadoOperacion> GuardarEventoAsync(
            Equipo? equipoLocal,
            Equipo? equipoVisitante,
            Estadio? estadio,
            string totalBoletosTexto,
            string precioTexto,
            DateTime fecha,
            TimeSpan hora)
        {
            // Invoca las validaciones del formulario guardando el mensaje de error si existiera
            var mensajeError = ValidarCampos(
                equipoLocal, equipoVisitante, estadio,
                totalBoletosTexto, precioTexto, fecha, hora);

            // Verifica si la cadena de error contiene información de fallo en las reglas
            if (mensajeError is not null)
                // Corta la ejecución devolviendo un objeto de resultado fallido para la interfaz
                return new ResultadoOperacion(exitoso: false, mensaje: mensajeError);

            // Realiza la conversión segura del texto a su representación numérica entera
            var totalBoletos = int.Parse(totalBoletosTexto);
            // Valida nuevamente el parseo del precio bajo la cultura invariante del sistema
            if (!decimal.TryParse(precioTexto, NumberStyles.Any, CultureInfo.InvariantCulture, out decimal precio))
                // Asigna el valor usando la cultura local del usuario en caso de fallo inicial
                decimal.TryParse(precioTexto, NumberStyles.Any, CultureInfo.CurrentCulture, out precio);
            // Construye el punto exacto en el tiempo sumando los componentes de fecha y hora
            var fechaHoraCompleta = fecha.Date + hora;

            // Inicializa el modelo de persistencia con los valores validados y formateados
            var nuevoEvento = new Evento
            {
                // Asocia la llave foránea del club que juega en casa
                LocalId      = equipoLocal!.Id,
                // Asocia la llave foránea del club que juega fuera de casa
                VisitanteId  = equipoVisitante!.Id,
                // Vincula el identificador del estadio seleccionado como sede
                EstadioId    = estadio!.Id,
                // Establece el límite total de boletaje disponible para venta
                TotalBoletos = totalBoletos,
                // Fija el valor unitario de acceso al recinto deportivo
                PrecioBoleto = precio,
                // Estandariza la fecha a la zonahoraria universal UTC para evitar desfases horarios
                FechaHoraPartido = fechaHoraCompleta.ToUniversalTime()
            };

            // Abre el bloque de control para interactuar directamente con el servidor de Supabase
            try
            {
                // Consulta el backend en búsqueda de partidos potencialmente duplicados
                var eventosExistentes = await SupabaseService.Cliente
                    // Apunta a la tabla de eventos en el esquema de la base de datos
                    .From<Evento>()
                    // Filtra los registros que coincidan exactamente con el equipo local
                    .Filter("local_id",      Operator.Equals, equipoLocal!.Id.ToString())
                    // Filtra los registros que coincidan exactamente con el equipo visitante
                    .Filter("visitante_id",  Operator.Equals, equipoVisitante!.Id.ToString())
                    // Filtra los registros que coincidan exactamente con la misma sede deportiva
                    .Filter("estadio_id",    Operator.Equals, estadio!.Id.ToString())
                    // Recupera los eventos que cumplen con los criterios anteriores
                    .Get();

                // Compara a nivel de lógica en memoria las fechas locales de los partidos coincidentes
                var mismaFecha = eventosExistentes.Models
                    // Evalúa si algún partido programado ocurre exactamente en la misma fecha calendario
                    .Any(ev => ev.FechaHoraPartido.ToLocalTime().Date == fechaHoraCompleta.Date);

                // Valida si se detectó un conflicto de programación idéntica
                if (mismaFecha)
                    // Interrumpe la inserción retornando un mensaje de aviso sobre la duplicidad
                    return new ResultadoOperacion(
                        exitoso: false,
                        mensaje: $"Ya existe un partido de {equipoLocal.Nombre} vs {equipoVisitante.Nombre} " +
                                 $"en {estadio.Nombre} para la misma fecha.");

                // Ejecuta la petición asíncrona para insertar el nuevo evento deportivo
                await SupabaseService.Cliente
                    // Apunta nuevamente a la colección de eventos de la plataforma
                    .From<Evento>()
                    // Lanza la orden de inserción del registro estructurado
                    .Insert(nuevoEvento);

                // Devuelve la respuesta exitosa concatenando los detalles del evento registrado
                return new ResultadoOperacion(
                    exitoso: true,
                    mensaje: $"✅ Partido registrado con éxito.\n" +
                             $"{equipoLocal.Nombre} vs {equipoVisitante.Nombre}\n" +
                             $"📍 {estadio.Nombre} — {fechaHoraCompleta:dd/MM/yyyy HH:mm}");
            }
            // Captura excepciones generadas por errores de sintaxis o problemas en la red
            catch (Exception ex)
            {
                // Registra la traza del error en el sistema de depuración
                Console.WriteLine($"[RegistroEventoController] Error al guardar evento: {ex.Message}");

                // Devuelve un mensaje de contingencia amigable solicitando revisión de conectividad
                return new ResultadoOperacion(
                    exitoso: false,
                    mensaje: "Ocurrió un error al guardar el evento. " +
                             "Verifica tu conexión a Internet e intenta nuevamente.");
            }
        }
    }

    // Esta estructura de datos especializada sirve como un contenedor ligero para transportar el estado final de las acciones del controlador hacia las capas visuales. Permite desacoplar la lógica de negocio de las alertas visuales de la interfaz, encapsulando tanto el éxito de la tarea como los mensajes descriptivos resultantes.
    public class ResultadoOperacion
    {
        // Determina si la operación se completó exitosamente o falló por alguna validación
        public bool Exitoso { get; }

        // Almacena el texto descriptivo o mensaje de error enfocado al usuario final
        public string Mensaje { get; }

        // Inicializa una nueva instancia del contenedor de resultados asociando el estado de éxito y el mensaje descriptivo correspondiente. Permite construir de manera rápida la respuesta que será interpretada por la vista para informar al usuario.
        public ResultadoOperacion(bool exitoso, string mensaje)
        {
            // Asigna la bandera de éxito al estado interno de la operación
            Exitoso = exitoso;
            // Asigna la cadena explicativa a la propiedad del mensaje de salida
            Mensaje = mensaje;
        }
    }
}
