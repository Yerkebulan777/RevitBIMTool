using System;

namespace Database.Models
{
    public sealed class PrinterReservation
    {
        public string PrinterName { get; set; }
        public string RevitFileName { get; set; }
        public DateTime ReservedAt { get; set; }
        public int ProcessId { get; set; }
        public Guid SessionId { get; set; }
        public ReservationState State { get; set; }
        public DateTime? CompletedAt { get; set; }
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