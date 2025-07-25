using System;

namespace Database.Models
{
    /// <summary>
    /// Модель блокировки принтера с расширенной информацией
    /// </summary>
    public class PrinterLock
    {
        public string PrinterName { get; set; }
        public string LockId { get; set; }
        public string ReservedBy { get; set; }
        public DateTime ReservedAt { get; set; }
        public DateTime ExpiresAt { get; set; }
        public string MachineName { get; set; }
        public bool IsActive => DateTime.UtcNow < ExpiresAt;
    }
}