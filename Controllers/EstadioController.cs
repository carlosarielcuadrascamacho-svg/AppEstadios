// Importa los modelos de datos de la aplicación para poder utilizar la clase Estadio
using AppEstadios.Models;

// Define el espacio de nombres para agrupar lógicamente los controladores del proyecto
namespace AppEstadios.Controllers
{
    // Esta clase actúa como el intermediario entre la interfaz de usuario y la base de datos para la gestión estadios.
    // Su propósito principal es centralizar la lógica de negocio relacionada con la validación, creación y almacenamiento de los
    // datos geográficos y descriptivos de los estadios en el sistema.
    public class EstadioController
    {
        // Este método procesa la solicitud de registro de un nuevo estadio, asegurando que la información cumpla con los requisitos
        // mínimos antes de persistirla. Se encarga de transformar los datos crudos en una entidad estructurada y gestionar la
        // comunicación asíncrona con el servicio de base de datos externo, retornando un estado claro del resultado de la operación.
        public async Task<(bool Exitoso, string Mensaje, Estadio? NuevoEstadio)> GuardarEstadioAsync(string nombre, double lat, double lon)
        {
            // Abre un bloque try para capturar cualquier excepción durante la comunicación con la base de datos
            try
            {
                // Valida si el nombre proporcionado es nulo, está vacío o contiene solo espacios en blanco
                if (string.IsNullOrWhiteSpace(nombre))
                    return (false, "El nombre del estadio no puede estar vacío.", null);

                // Verificar que no exista ya un estadio con el mismo nombre
                var existentes = await SupabaseService.Cliente
                    .From<Estadio>()
                    .Get();

                var yaExiste = existentes.Models
                    .Any(e => string.Equals(e.Nombre.Trim(), nombre.Trim(), StringComparison.OrdinalIgnoreCase));

                if (yaExiste)
                    return (false, $"Ya existe un estadio llamado \"{nombre}\". Elige un nombre diferente.", null);

                // Crea una nueva instancia del modelo Estadio mapeando los parámetros recibidos
                var nuevoEstadio = new Estadio
                {
                    Nombre   = nombre,
                    Latitud  = lat,
                    Longitud = lon
                };

                // Conecta con el cliente de Supabase para enviar el nuevo registro a la base de datos en la nube
                var response = await SupabaseService.Cliente
                    .From<Estadio>()
                    .Insert(nuevoEstadio);


                // Recupera el primer elemento de la lista de modelos devueltos por la respuesta de Supabase
                var estadioInsertado = response.Models.FirstOrDefault();

                // Verifica si el objeto se guardó y retornó correctamente desde el servidor
                if (estadioInsertado != null)
                {
                    // Retorna una tupla con éxito, un mensaje informativo y la entidad persistida
                    return (true, "Estadio agregado correctamente.", estadioInsertado);
                }

                // Maneja el escenario donde la base de datos no devolvió el registro creado
                return (false, "No se pudo recuperar el estadio insertado.", null);
            }
            // Captura cualquier error inesperado que ocurra durante el proceso de guardado
            catch (Exception ex)
            {
                // Devuelve el estado fallido adjuntando la descripción exacta del error ocurrido
                return (false, $"Error al guardar el estadio: {ex.Message}", null);
            }
        }
    }
}
