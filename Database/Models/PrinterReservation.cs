using System;

namespace Database.Models
{
    /// <summary>
    /// Объединенная модель для резервации и зависших принтеров
    /// </summary>
    public sealed class PrinterReservation
    {
        public int Id { get; set; }
        public string PrinterName { get; set; }
        public string RevitFileName { get; set; }
        public int? ProcessId { get; set; }
        public Guid SessionId { get; set; }
        public DateTime LastUpdate { get; set; }
        public DateTime ReservedAt { get; set; }
        public DateTime? CompletedAt { get; set; }
        public ReservationState State { get; set; }

        /// <summary>
        /// Минуты с момента резервирования (для поиска зависших)
        /// </summary>
        public double MinutesStuck { get; set; }

        /// <summary>
        /// Проверяет, завис ли принтер
        /// </summary>
        public bool IsStuck(TimeSpan threshold) =>
            State == ReservationState.InProgress &&
            DateTime.UtcNow - ReservedAt > threshold;
    }

    public enum ReservationState
    {
        Reserved = 0,
        InProgress = 1,
        Completed = 2,
        Failed = 3,
        Compensated = 4
    }


}