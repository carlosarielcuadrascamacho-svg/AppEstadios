using Postgrest.Attributes;
using Postgrest.Models;

namespace AppEstadios.Models
{
    /// <summary>
    /// Representa un equipo de fútbol.
    /// Mapea a la tabla "equipos" en Supabase (PostgreSQL).
    /// </summary>
    [Table("equipos")]
    public class Equipo : BaseModel
    {
        /// <summary>
        /// Identificador único del equipo (clave primaria en BD).
        /// </summary>
        [PrimaryKey("id", false)]
        public int Id { get; set; }

        /// <summary>
        /// Nombre oficial del equipo (ej: "Real Madrid", "FC Barcelona").
        /// </summary>
        [Column("nombre")]
        public string Nombre { get; set; } = string.Empty;

        /// <summary>
        /// URL pública de la imagen del logo del equipo (almacenada en Supabase Storage).
        /// </summary>
        [Column("logo_url")]
        public string LogoUrl { get; set; } = string.Empty;

        /// <summary>
        /// Sobrescribimos ToString() para que el Picker muestre
        /// el nombre del equipo directamente en la UI.
        /// </summary>
        public override string ToString() => Nombre;
    }
}
