using AppEstadios.Models;
using static Postgrest.Constants;

namespace AppEstadios.Controllers
{
    public class RegistroEventoController
    {
        
        public async Task<List<Equipo>> ObtenerEquiposAsync()
        {
            try
            {
                var respuesta = await SupabaseService.Cliente
                    .From<Equipo>()
                    .Order("nombre", Ordering.Ascending)
                    .Get();

                // Retornamos la lista de modelos obtenidos
                return respuesta.Models;
            }
            catch (Exception ex)
            {
                // Registramos el error para diagnóstico sin romper la app
                Console.WriteLine($"[RegistroEventoController] Error al cargar equipos: {ex.Message}");

                // Retornamos lista vacía — la View mostrará los Pickers vacíos
                return new List<Equipo>();
            }
        }

        /// <summary>
        /// Obtiene la lista completa de estadios desde Supabase.
        /// La View usará esta lista para llenar el Picker de estadios.
        ///
        /// RETORNA: Lista de objetos Estadio. Lista vacía si ocurre un error.
        /// </summary>
        public async Task<List<Estadio>> ObtenerEstadiosAsync()
        {
            try
            {
                // Realizamos la consulta SELECT * FROM estadios ORDER BY nombre
                var respuesta = await SupabaseService.Cliente
                    .From<Estadio>()
                    .Order("nombre", Ordering.Ascending)
                    .Get();

                return respuesta.Models;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[RegistroEventoController] Error al cargar estadios: {ex.Message}");
                return new List<Estadio>();
            }
        }

        // ──────────────────────────────────────────────────────────
        //  PASO 2: VALIDACIÓN DE FORMULARIO
        // ──────────────────────────────────────────────────────────

        /// <summary>
        /// Valida que todos los campos del formulario tengan valores correctos
        /// ANTES de intentar guardar en la base de datos.
        ///
        /// PARÁMETROS: Los datos en bruto tal como los lee la View desde la UI.
        /// RETORNA: null si todo es válido, o un string con el mensaje de error.
        ///
        /// PATÓN: "Early return" — retorna en cuanto encuentra el primer error.
        /// </summary>
        private string? ValidarCampos(
            Equipo? equipoLocal,
            Equipo? equipoVisitante,
            Estadio? estadio,
            string totalBoletosTexto,
            string precioTexto,
            DateTime fecha,
            TimeSpan hora)
        {
            // ── Validación 1: Equipo local seleccionado ──
            if (equipoLocal is null)
                return "Debes seleccionar el equipo local.";

            // ── Validación 2: Equipo visitante seleccionado ──
            if (equipoVisitante is null)
                return "Debes seleccionar el equipo visitante.";

            // ── Validación 3: No puede ser el mismo equipo ──
            if (equipoLocal.Id == equipoVisitante.Id)
                return "El equipo local y visitante no pueden ser el mismo.";

            // ── Validación 4: Estadio seleccionado ──
            if (estadio is null)
                return "Debes seleccionar un estadio.";

            // ── Validación 5: Total de boletos es un número positivo ──
            if (!int.TryParse(totalBoletosTexto, out int totalBoletos) || totalBoletos <= 0)
                return "El total de boletos debe ser un número mayor a cero.";

            // ── Validación 6: Precio es un número decimal positivo ──
            if (!decimal.TryParse(precioTexto, out decimal precio) || precio <= 0)
                return "El precio del boleto debe ser un valor mayor a cero.";

            // ── Validación 7: La fecha y hora del partido debe ser futura ──
            // Combinamos fecha y hora para obtener el DateTime completo
            var fechaHoraCompleta = fecha.Date + hora;
            if (fechaHoraCompleta <= DateTime.Now)
                return "La fecha y hora del partido debe ser en el futuro.";

            // Si pasó todas las validaciones, retornamos null (sin error)
            return null;
        }

        // ──────────────────────────────────────────────────────────
        //  PASO 3: GUARDAR EVENTO EN SUPABASE
        // ──────────────────────────────────────────────────────────

        /// <summary>
        /// Método principal que orquesta la validación y el guardado del evento.
        /// Es el método que la View llama cuando el usuario toca "REGISTRAR PARTIDO".
        ///
        /// PARÁMETROS:
        ///   equipoLocal      — Objeto Equipo seleccionado en el Picker local.
        ///   equipoVisitante  — Objeto Equipo seleccionado en el Picker visitante.
        ///   estadio          — Objeto Estadio seleccionado en el Picker de estadios.
        ///   totalBoletosTexto— Texto del campo Entry de boletos (sin parsear).
        ///   precioTexto      — Texto del campo Entry de precio (sin parsear).
        ///   fecha            — Valor del DatePicker (solo fecha, sin hora).
        ///   hora             — Valor del TimePicker (solo hora).
        ///
        /// RETORNA: ResultadoOperacion con éxito y mensaje para el usuario.
        ///
        /// DISEÑO: Retornamos un objeto resultado en vez de lanzar excepciones
        ///   para que la View pueda mostrar alertas amigables sin try/catch en la UI.
        /// </summary>
        public async Task<ResultadoOperacion> GuardarEventoAsync(
            Equipo? equipoLocal,
            Equipo? equipoVisitante,
            Estadio? estadio,
            string totalBoletosTexto,
            string precioTexto,
            DateTime fecha,
            TimeSpan hora)
        {
            // ── Paso A: Validar los datos ──
            var mensajeError = ValidarCampos(
                equipoLocal, equipoVisitante, estadio,
                totalBoletosTexto, precioTexto, fecha, hora);

            // Si hay error de validación, retornamos sin tocar la BD
            if (mensajeError is not null)
                return new ResultadoOperacion(exitoso: false, mensaje: mensajeError);

            // ── Paso B: Parsear los valores numéricos (ya validados) ──
            var totalBoletos = int.Parse(totalBoletosTexto);
            var precio = decimal.Parse(precioTexto);
            var fechaHoraCompleta = fecha.Date + hora;

            // ── Paso C: Construir el objeto Evento a insertar ──
            var nuevoEvento = new Evento
            {
                LocalId      = equipoLocal!.Id,
                VisitanteId  = equipoVisitante!.Id,
                EstadioId    = estadio!.Id,
                TotalBoletos = totalBoletos,
                PrecioBoleto = precio,
                // Convertimos a UTC antes de guardar en BD (buena práctica)
                FechaHoraPartido = fechaHoraCompleta.ToUniversalTime()
            };

            try
            {
                // ── Paso D: Realizar el INSERT en Supabase ──
                // El método Insert() envía un POST a la API REST de Supabase
                await SupabaseService.Cliente
                    .From<Evento>()
                    .Insert(nuevoEvento);

                // ── Paso E: Retornar éxito ──
                return new ResultadoOperacion(
                    exitoso: true,
                    mensaje: $"✅ Partido registrado con éxito.\n" +
                             $"{equipoLocal.Nombre} vs {equipoVisitante.Nombre}\n" +
                             $"📍 {estadio.Nombre} — {fechaHoraCompleta:dd/MM/yyyy HH:mm}");
            }
            catch (Exception ex)
            {
                // ── Paso F: Capturar errores de red/BD y retornar mensaje amigable ──
                Console.WriteLine($"[RegistroEventoController] Error al guardar evento: {ex.Message}");

                return new ResultadoOperacion(
                    exitoso: false,
                    mensaje: "Ocurrió un error al guardar el evento. " +
                             "Verifica tu conexión a Internet e intenta nuevamente.");
            }
        }
    }

    // ──────────────────────────────────────────────────────────
    //  CLASE AUXILIAR: ResultadoOperacion
    // ──────────────────────────────────────────────────────────

    /// <summary>
    /// Objeto de transferencia simple (DTO) que encapsula el resultado
    /// de una operación del controlador hacia la View.
    ///
    /// Evita que la View tenga que manejar try/catch o excepciones directamente.
    ///
    /// USO EN LA VIEW:
    ///   var resultado = await _controlador.GuardarEventoAsync(...);
    ///   if (resultado.Exitoso)
    ///       await DisplayAlert("Éxito", resultado.Mensaje, "OK");
    ///   else
    ///       await DisplayAlert("Atención", resultado.Mensaje, "OK");
    /// </summary>
    public class ResultadoOperacion
    {
        /// <summary>
        /// true si la operación fue exitosa, false si hubo error o validación fallida.
        /// </summary>
        public bool Exitoso { get; }

        /// <summary>
        /// Mensaje amigable para mostrar al usuario en un DisplayAlert.
        /// </summary>
        public string Mensaje { get; }

        public ResultadoOperacion(bool exitoso, string mensaje)
        {
            Exitoso = exitoso;
            Mensaje = mensaje;
        }
    }
}
