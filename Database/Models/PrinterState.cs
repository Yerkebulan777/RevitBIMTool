using System;

namespace Database.Models
{
    public class PrinterState
    {
        public int Id { get; set; }

        /// <summary>
        /// Уникальное имя принтера (первичный ключ для бизнес-логики)
        /// </summary>
        public string PrinterName { get; set; }

        /// <summary>
        /// Доступен ли принтер для резервирования
        /// </summary>
        public bool IsAvailable { get; set; }

        /// <summary>
        /// Кто зарезервировал принтер (процесс или пользователь)
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
        /// ID процесса, который зарезервировал принтер
        /// </summary>
        public int? ProcessId { get; set; }

        /// <summary>
        /// Имя машины для распределенной работы
        /// </summary>
        public string MachineName { get; set; }

        /// <summary>
        /// Версия строки для оптимистичного блокирования
        /// Предотвращает потерю обновлений при конкурентном доступе
        /// </summary>
        public long Version { get; set; }
    }



}