using Postgrest.Attributes;
using Postgrest.Models;

namespace AppEstadios.Models
{
    [Table("equipos")]
    public class Equipo : BaseModel
    {
        [PrimaryKey("id", false)]
        public int Id { get; set; }

        [Column("nombre")]
        public string Nombre { get; set; } = string.Empty;

        [Column("logo_url")]
        public string LogoUrl { get; set; } = string.Empty;

        public override string ToString() => Nombre;
    }
}
