using System;

namespace Database.Models
{
    public sealed class PrinterState
    {
        /// <summary>
        /// Уникальный идентификатор
        /// </summary>
        public int Id { get; set; }

        /// <summary>
        /// Имя принтера
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
        /// Когда был зарезервирован
        /// </summary>
        public DateTime? ReservedAt { get; set; }

        /// <summary>
        /// ID процесса для отслеживания зависших блокировок
        /// </summary>
        public int? ProcessId { get; set; }

        /// <summary>
        /// Версия для оптимистичного блокирования
        /// Ключевое поле для предотвращения race conditions
        /// </summary>
        public long Version { get; set; }
    }



}