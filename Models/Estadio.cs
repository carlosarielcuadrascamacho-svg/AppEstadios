using Postgrest.Attributes;
using Postgrest.Models;

namespace AppEstadios.Models
{
    // Representa un estadio de fútbol con sus coordenadas geográficas.
    // Mapea a la tabla "estadios" en Supabase (PostgreSQL).
    [Table("estadios")]
    public class Estadio : BaseModel
    {
        // Identificador único del estadio (clave primaria en BD).
        [PrimaryKey("id", false)]
        public int Id { get; set; }

        // Nombre oficial del estadio (ej: "Camp Nou", "Estadio Azteca").
        [Column("nombre")]
        public string Nombre { get; set; } = string.Empty;

        // Latitud geográfica del estadio para integración con mapas.
        [Column("latitud")]
        public double Latitud { get; set; }

        // Longitud geográfica del estadio para integración con mapas.
        [Column("longitud")]
        public double Longitud { get; set; }

        // Sobrescribimos ToString() para que el Picker muestre
        // el nombre del estadio directamente en la UI.
        public override string ToString() => Nombre;
    }
}
