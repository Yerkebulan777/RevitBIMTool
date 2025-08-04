using Database.Models;
using Database.Services;
using RevitBIMTool.Models;
using RevitBIMTool.Utils.ExportPDF.Printers;
using Serilog;
using System.Configuration;

namespace RevitBIMTool.Utils.ExportPDF
{
    internal static class SafePrintManager
    {
        private static BackgroundCleanupService _cleanupService;
        private static readonly object _lock = new();

        static SafePrintManager()
        {
            InitializeCleanupService();
        }

        private static void InitializeCleanupService()
        {
            lock (_lock)
            {
                if (_cleanupService == null)
                {
                    string connectionString = ConfigurationManager.ConnectionStrings["PrinterDatabase"]?.ConnectionString;

                    if (!string.IsNullOrEmpty(connectionString))
                    {
                        _cleanupService = new BackgroundCleanupService(connectionString, TimeSpan.FromMinutes(30));
                    }
                }
            }
        }

        public static bool ExecuteSafePrinting(
            Func<PrinterControl, List<SheetModel>> printOperation,
            string revitFilePath,
            out List<SheetModel> result,
            out PrinterControl usedPrinter)
        {
            result = null;
            usedPrinter = null;

            string connectionString = ConfigurationManager
                .ConnectionStrings["PrinterDatabase"]?.ConnectionString;

            using IDisposable monitor = TransactionMonitor.Instance
                .BeginMonitoring("PDF Export", revitFilePath);

            using PrinterReservationService reservationService = new(connectionString);

            PrinterReservation reservation = null;

            try
            {
                // Шаг 1: Быстрое резервирование принтера
                if (!PrinterManager.TryGetPrinter(revitFilePath, out PrinterControl printer))
                {
                    Log.Error("No printer available");
                    return false;
                }

                usedPrinter = printer;
                reservation = reservationService.ReservePrinter(
                    printer.PrinterName, revitFilePath);

                if (reservation == null)
                {
                    Log.Error("Failed to reserve printer");
                    return false;
                }

                // Шаг 2: Выполнение печати с периодическим обновлением статуса
                reservationService.UpdateProgress(
                    printer.PrinterName, ReservationState.InProgress);

                result = printOperation(printer);

                // Шаг 3: Освобождение принтера
                reservationService.ReleasePrinter(printer.PrinterName, true);

                Log.Information("Printing completed successfully");
                return true;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Printing failed");

                // Компенсация при ошибке
                if (reservation != null)
                {
                    reservationService.CompensateFailedReservation(reservation);
                }

                return false;
            }
            finally
            {
                // Восстановление настроек принтера
                usedPrinter?.RestoreDefaultSettings();
            }
        }
    }
}