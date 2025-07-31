using Database.Models;

namespace Database
{
    public interface IPrinterCommandService
    {
        void UpdatePrinterStatus(int printerId, PrinterInfo state);
        void HandleStuckPrinter(int printerId);
        void HandleStuckPrinters();
        void LogError(int printerId, string errorMessage);
        void ResetPrinter(int printerId);
    }
}
