using Postgrest.Attributes;
using Postgrest.Models;

namespace AppEstadios.Models
{
    // Representa un evento (partido de fútbol) registrado en el sistema.
    // Mapea a la tabla "eventos" en Supabase (PostgreSQL).
    // Contiene llaves foráneas hacia Equipo y Estadio.
    [Table("eventos")]
    public class Evento : BaseModel
    {
        /// Identificador único del evento (clave primaria, generada por la BD).
        [PrimaryKey("id", false)]
        public int Id { get; set; }

        
        // Llave foránea: ID del equipo que juega como LOCAL.
        // Referencia a la tabla "equipos".
        [Column("local_id")]
        public int LocalId { get; set; }

        // Llave foránea: ID del equipo que juega como VISITANTE.
        // Referencia a la tabla "equipos".
        [Column("visitante_id")]
        public int VisitanteId { get; set; }

        // Llave foránea: ID del estadio donde se jugará el partido.
        // Referencia a la tabla "estadios".
        [Column("estadio_id")]
        public int EstadioId { get; set; }

        // Número total de boletos disponibles para este evento.
      
        [Column("total_boletos")]
        public int TotalBoletos { get; set; }

        // Precio unitario de cada boleto en la moneda local (MXN/USD).
        // Usamos decimal para evitar errores de redondeo en valores monetarios.
        [Column("precio_boleto")]
        public decimal PrecioBoleto { get; set; }

        
        // Fecha y hora exacta en que se jugará el partido.
        // Se almacena como UTC en la BD y se convierte al mostrar en UI.
        [Column("fecha_hora_partido")]
        public DateTime FechaHoraPartido { get; set; }
    }
}
