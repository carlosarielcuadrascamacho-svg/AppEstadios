using AppEstadios.Models;

namespace AppEstadios.Controllers
{
    public class EstadioController
    {
        public async Task<(bool Exitoso, string Mensaje, Estadio? NuevoEstadio)> GuardarEstadioAsync(string nombre, double lat, double lon)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(nombre))
                    return (false, "El nombre del estadio no puede estar vacío.", null);

                var nuevoEstadio = new Estadio
                {
                    Nombre = nombre,
                    Latitud = lat,
                    Longitud = lon
                };

                var response = await SupabaseService.Cliente
                    .From<Estadio>()
                    .Insert(nuevoEstadio);

                var estadioInsertado = response.Models.FirstOrDefault();

                if (estadioInsertado != null)
                {
                    return (true, "Estadio agregado correctamente.", estadioInsertado);
                }

                return (false, "No se pudo recuperar el estadio insertado.", null);
            }
            catch (Exception ex)
            {
                return (false, $"Error al guardar el estadio: {ex.Message}", null);
            }
        }
    }
}
