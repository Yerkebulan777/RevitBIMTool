using System;

namespace Database.Models
{
    public sealed class PrinterState
    {
        /// <summary>
        /// Уникальный идентификатор записи в базе данных
        /// </summary>
        public int Id { get; set; }

        /// <summary>
        /// Имя принтера в операционной системе
        /// </summary>
        public string PrinterName { get; set; }

        /// <summary>
        /// Доступен ли принтер для резервирования в данный момент
        /// true = свободен, false = занят
        /// </summary>
        public bool IsAvailable { get; set; }

        /// <summary>
        /// Имя файла Revit, который зарезервировал этот принтер
        /// NULL если принтер свободен
        /// </summary>
        public string ReservedByFile { get; set; }

        /// <summary>
        /// Время резервирования принтера
        /// Используется для автоматической очистки зависших блокировок
        /// NULL если принтер свободен
        /// </summary>
        public DateTime? ReservedAt { get; set; }

        /// <summary>
        /// ID процесса Revit, который зарезервировал принтер
        /// Помогает определить зависшие процессы для cleanup
        /// NULL если принтер свободен
        /// </summary>
        public int? ProcessId { get; set; }

        /// <summary>
        /// Токен изменения для оптимистичного блокирования
        /// Предотвращает конфликты при одновременном доступе к принтеру
        /// </summary>
        public Guid ChangeToken { get; set; }

    }
}