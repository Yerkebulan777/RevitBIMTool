using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Database.Extensions;
using Database.Models;
using Database.Providers;
using Database.Services;
using RevitBIMTool.Utils.Database;
using System.Globalization;
using System.Text;
using System.Windows;

namespace RevitBIMTool.Commands
{
    [Transaction(TransactionMode.Manual)]
    public class TestCommand : IExternalCommand, IExternalCommandAvailability
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            Thread.CurrentThread.CurrentCulture = CultureInfo.InvariantCulture;

            StringBuilder report = new();

            try
            {
                // Регистрируем PostgreSQL провайдер для основного проекта
                string connectionString = ConfigurationHelper.GetPrinterConnectionString();
                string provider = ConfigurationHelper.GetDatabaseProvider();

                _ = report.AppendLine($"Provider: {provider}");
                _ = report.AppendLine($"Connection: {connectionString}");
                _ = report.AppendLine();

                if (provider.Equals("PostgreSQL", StringComparison.OrdinalIgnoreCase))
                {
                    // Регистрируем PostgreSQL провайдер
                    DatabaseProviderFactory.RegisterProvider("postgresql", () => new ConcretePostgreSqlProvider());

                    _ = report.AppendLine("✓ PostgreSQL provider registered");
                }

                // Инициализируем систему принтеров
                IPrinterStateService printerService = DatabaseExtensions.InitializePrinterSystem(connectionString);
                _ = report.AppendLine("✓ Printer service initialized");

                // Тестируем получение принтеров
                IEnumerable<PrinterState> printers = printerService.GetAllPrinters();
                _ = report.AppendLine($"✓ Found {System.Linq.Enumerable.Count(printers)} printers");

                foreach (PrinterState printer in printers)
                {
                    string status = printer.IsAvailable ? "Available" : $"Reserved by {printer.ReservedBy}";
                    _ = report.AppendLine($"  - {printer.PrinterName}: {status}");
                }

                // Тестируем резервирование
                string[] preferredPrinters = { "PDF Writer - bioPDF", "PDF24" };
                string reservedPrinter = printerService.TryReserveAnyAvailablePrinter("TestUser", preferredPrinters);

                if (!string.IsNullOrEmpty(reservedPrinter))
                {
                    _ = report.AppendLine($"✓ Reserved printer: {reservedPrinter}");
                    bool released = printerService.ReleasePrinter(reservedPrinter);
                    _ = report.AppendLine($"✓ Released printer: {released}");
                }
                else
                {
                    _ = report.AppendLine("⚠ No printers available for reservation");
                }

                _ = TaskDialog.Show("Database Test Results", report.ToString());

                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                _ = report.AppendLine($"✗ Error: {ex.Message}");

                if (ex.InnerException != null)
                {
                    _ = report.AppendLine($"Inner: {ex.InnerException.Message}");
                }

                Clipboard.SetText(report.ToString());
                _ = TaskDialog.Show("Database Test Failed", report.ToString());

                return Result.Failed;
            }
        }

        public bool IsCommandAvailable(UIApplication applicationData, CategorySet selectedCategories)
        {
            return applicationData?.ActiveUIDocument != null;
        }
    }
}