using Database.Models;
using System;

namespace Database.Services
{
    public interface IPrinterStateManager
    {
        bool IsPrinterStuck(PrinterInfo printer);
        bool IsStatusUpdateNeeded(PrinterInfo printer);
        TimeSpan StuckThreshold { get; }
    }


    public class PrinterStateManager : IPrinterStateManager
    {
        public TimeSpan StuckThreshold { get; set; } = TimeSpan.FromMinutes(30);

        /// <summary>
        /// Проверяет, завис ли принтер
        /// </summary>
        public bool IsPrinterStuck(PrinterInfo printer)
        {
            return printer.State == PrinterState.Printing && printer.LastUpdate < DateTime.UtcNow.Subtract(StuckThreshold);
        }

        /// <summary>
        /// Проверяет, нужно ли обновить статус принтера
        /// </summary>
        public bool IsStatusUpdateNeeded(PrinterInfo printer)
        {
            return printer.LastUpdate < DateTime.UtcNow.AddMinutes(-5);
        }


    }
}
