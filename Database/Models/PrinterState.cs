using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RevitBIMTool.Database.Models
{
    /// <summary>
    /// Модель состояния принтера в базе данных
    /// Использует оптимистичное блокирование через RowVersion
    /// </summary>
    [Table("printer_states")]

    public class PrinterState
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Required]
        [MaxLength(100)]
        [Column("printer_name")]
        public string PrinterName { get; set; }

        [Column("is_available")]
        public bool IsAvailable { get; set; }

        [Column("reserved_by")]
        [MaxLength(100)]
        public string ReservedBy { get; set; }

        [Column("reserved_at")]
        public DateTime? ReservedAt { get; set; }

        [Column("last_updated")]
        public DateTime LastUpdated { get; set; }

        [Column("process_id")]
        public int? ProcessId { get; set; }

        [Column("machine_name")]
        [MaxLength(50)]
        public string MachineName { get; set; }

        /// <summary>
        /// Версия строки для оптимистичного блокирования
        /// Предотвращает конфликты при одновременном доступе
        /// </summary>
        [Timestamp]
        [Column("row_version")]
        public byte[] RowVersion { get; set; }
    }
}