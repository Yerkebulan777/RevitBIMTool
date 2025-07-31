using System;

namespace Database.Models
{
    public enum PrinterState
    {
        Ready = 0,
        Printing = 1,
        Paused = 2,
        Error = 3
    }

    public sealed class PrinterInfo
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
        /// Имя файла, который зарезервировал этот принтер
        /// NULL если принтер свободен
        /// </summary>
        public string ReservedFileName { get; set; }

        /// <summary>
        /// Время резервирования принтера
        /// Используется для автоматической очистки зависших блокировок
        /// NULL если принтер свободен
        /// </summary>
        public DateTime LastUpdate { get; set; }

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
        public Guid VersionToken { get; set; }

        /// <summary>
        /// Количество заданий в очереди на печать для этого принтера
        /// </summary>
        public int JobCount { get; set; }

        /// <summary>
        /// Cостояние принтера
        /// </summary>
        public PrinterState State { get; set; }

    }
}