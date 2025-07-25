using System;

namespace Database.Models
{
    /// <summary>
    /// Простая модель состояния принтера без лишних украшений
    /// Dapper автоматически маппит поля из PostgreSQL
    /// </summary>
    public class PrinterState
    {
        public int Id { get; set; }

        /// <summary>
        /// Имя принтера - основной ключ для бизнес-логики
        /// </summary>
        public string PrinterName { get; set; }

        /// <summary>
        /// Доступен ли принтер для резервирования
        /// </summary>
        public bool IsAvailable { get; set; }

        /// <summary>
        /// Кто зарезервировал принтер
        /// </summary>
        public string ReservedBy { get; set; }

        /// <summary>
        /// Когда был зарезервирован (для автоматического освобождения)
        /// </summary>
        public DateTime? ReservedAt { get; set; }

        /// <summary>
        /// Последнее обновление записи
        /// </summary>
        public DateTime LastUpdated { get; set; }

        /// <summary>
        /// ID процесса для отслеживания зависших блокировок
        /// </summary>
        public int? ProcessId { get; set; }

        /// <summary>
        /// Имя машины для распределенной работы
        /// </summary>
        public string MachineName { get; set; }

        /// <summary>
        /// Версия для оптимистичного блокирования
        /// Ключевое поле для предотвращения race conditions
        /// </summary>
        public long Version { get; set; }
    }
}