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
                        TimeSpan cleanupTimeout = TimeSpan.FromMinutes(30);
                        _cleanupService = new BackgroundCleanupService(connectionString, cleanupTimeout);
                    }
                }
            }
        }

        public static bool ExecuteSafePrinting(
            Func<PrinterControl, List<SheetModel>> operation,
            string revitFilePath,
            out List<SheetModel> result,
            out PrinterControl usedPrinter)
        {
            result = null;
            usedPrinter = null;

            try
            {
                // Шаг 1: Быстрое резервирование принтера
                if (!PrinterManager.TryGetPrinter(revitFilePath, out PrinterControl printer))
                {
                    Log.Error("No printer available");
                    return false;
                }

                usedPrinter = printer;

                // Шаг 2: Выполнение печати
                result = operation(printer);

                Log.Information("Printing completed successfully");
                return true;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Printing failed");
                return false;
            }
            finally
            {
                // Освобождение принтера
                if (usedPrinter != null)
                {
                    PrinterManager.ReleasePrinter(usedPrinter);
                }
            }
        }
    }
}