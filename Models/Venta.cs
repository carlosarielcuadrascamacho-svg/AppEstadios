using Postgrest.Attributes;
using Postgrest.Models;

namespace AppEstadios.Models
{
    /// <summary>
    /// Representa una venta de boletos para un evento específico.
    /// Mapea a la tabla "ventas" en Supabase (PostgreSQL).
    ///
    /// RELACIONES:
    ///   - Pertenece a un Evento (evento_id → eventos.id)
    ///   - Guarda los datos del comprador directamente (sin tabla cliente separada)
    /// </summary>
    [Table("ventas")]
    public class Venta : BaseModel
    {
        /// <summary>
        /// Identificador único de la venta (clave primaria, auto-generada por BD).
        /// </summary>
        [PrimaryKey("id", false)]
        public int Id { get; set; }

        /// <summary>
        /// Llave foránea: ID del evento al que pertenecen los boletos vendidos.
        /// Referencia a la tabla "eventos".
        /// </summary>
        [Column("evento_id")]
        public int EventoId { get; set; }

        /// <summary>
        /// Nombre completo del cliente comprador.
        /// </summary>
        [Column("nombre_cliente")]
        public string NombreCliente { get; set; } = string.Empty;

        /// <summary>
        /// Número de teléfono de contacto del cliente.
        /// </summary>
        [Column("telefono")]
        public string Telefono { get; set; } = string.Empty;

        /// <summary>
        /// Cantidad de boletos adquiridos en esta transacción.
        /// </summary>
        [Column("cantidad_boletos")]
        public int CantidadBoletos { get; set; }

        /// <summary>
        /// Total cobrado en esta venta (calculado como: cantidad × precio_boleto del evento).
        /// Se guarda desnormalizado para facilitar reportes sin JOIN.
        /// </summary>
        [Column("total_cobrado")]
        public decimal TotalCobrado { get; set; }

        /// <summary>
        /// Fecha y hora exacta en que se realizó la transacción.
        /// Se asigna automáticamente al crear la venta.
        /// </summary>
        [Column("fecha_venta")]
        public DateTime FechaVenta { get; set; }
    }
}
