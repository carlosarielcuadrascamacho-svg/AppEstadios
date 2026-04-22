using Postgrest.Attributes;
using Postgrest.Models;

namespace AppEstadios.Models
{
    /// <summary>
    /// Representa un estadio de fútbol con sus coordenadas geográficas.
    /// Mapea a la tabla "estadios" en Supabase (PostgreSQL).
    /// </summary>
    [Table("estadios")]
    public class Estadio : BaseModel
    {
        /// <summary>
        /// Identificador único del estadio (clave primaria en BD).
        /// </summary>
        [PrimaryKey("id", false)]
        public int Id { get; set; }

        /// <summary>
        /// Nombre oficial del estadio (ej: "Camp Nou", "Estadio Azteca").
        /// </summary>
        [Column("nombre")]
        public string Nombre { get; set; } = string.Empty;

        /// <summary>
        /// Latitud geográfica del estadio para integración con mapas.
        /// </summary>
        [Column("latitud")]
        public double Latitud { get; set; }

        /// <summary>
        /// Longitud geográfica del estadio para integración con mapas.
        /// </summary>
        [Column("longitud")]
        public double Longitud { get; set; }

        /// <summary>
        /// Sobrescribimos ToString() para que el Picker muestre
        /// el nombre del estadio directamente en la UI.
        /// </summary>
        public override string ToString() => Nombre;
    }
}
