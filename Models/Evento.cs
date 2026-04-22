using Postgrest.Attributes;
using Postgrest.Models;

namespace AppEstadios.Models
{
    /// <summary>
    /// Representa un evento (partido de fútbol) registrado en el sistema.
    /// Mapea a la tabla "eventos" en Supabase (PostgreSQL).
    /// Contiene llaves foráneas hacia Equipo y Estadio.
    /// </summary>
    [Table("eventos")]
    public class Evento : BaseModel
    {
        /// <summary>
        /// Identificador único del evento (clave primaria, generada por la BD).
        /// </summary>
        [PrimaryKey("id", false)]
        public int Id { get; set; }

        /// <summary>
        /// Llave foránea: ID del equipo que juega como LOCAL.
        /// Referencia a la tabla "equipos".
        /// </summary>
        [Column("local_id")]
        public int LocalId { get; set; }

        /// <summary>
        /// Llave foránea: ID del equipo que juega como VISITANTE.
        /// Referencia a la tabla "equipos".
        /// </summary>
        [Column("visitante_id")]
        public int VisitanteId { get; set; }

        /// <summary>
        /// Llave foránea: ID del estadio donde se jugará el partido.
        /// Referencia a la tabla "estadios".
        /// </summary>
        [Column("estadio_id")]
        public int EstadioId { get; set; }

        /// <summary>
        /// Número total de boletos disponibles para este evento.
        /// </summary>
        [Column("total_boletos")]
        public int TotalBoletos { get; set; }

        /// <summary>
        /// Precio unitario de cada boleto en la moneda local (MXN/USD).
        /// Usamos decimal para evitar errores de redondeo en valores monetarios.
        /// </summary>
        [Column("precio_boleto")]
        public decimal PrecioBoleto { get; set; }

        /// <summary>
        /// Fecha y hora exacta en que se jugará el partido.
        /// Se almacena como UTC en la BD y se convierte al mostrar en UI.
        /// </summary>
        [Column("fecha_hora_partido")]
        public DateTime FechaHoraPartido { get; set; }
    }
}
