
using Database;
using RevitBIMTool.Utils.Common;
using RevitBIMTool.Utils.ExportPDF.Printers;
using RevitBIMTool.Utils.SystemHelpers;
using Serilog;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;

namespace RevitBIMTool.Utils.ExportPDF
{
    internal static class PrinterStateManager
    {
        private static readonly object _lockObject = new object();
        private static SafePostgreSqlPrinterService _printerService;
        private static readonly string _connectionString;
        private static readonly int _lockTimeoutMinutes;
        private static readonly bool _useDatabaseProvider;

        static PrinterStateManager()
        {
            // Проверяем настройки для использования БД
            _useDatabaseProvider = string.Equals(
                ConfigurationManager.AppSettings["DefaultDatabaseProvider"],
                "PostgreSQL",
                StringComparison.OrdinalIgnoreCase);

            if (_useDatabaseProvider)
            {
                _connectionString = ConfigurationManager.ConnectionStrings["PrinterDatabase"]?.ConnectionString;
                _lockTimeoutMinutes = int.TryParse(
                    ConfigurationManager.AppSettings["PrinterLockTimeoutMinutes"],
                    out int timeout) ? timeout : 10;

                if (string.IsNullOrEmpty(_connectionString))
                {
                    Log.Warning("PrinterDatabase connection string not found, falling back to XML storage");
                    _useDatabaseProvider = false;
                }
            }
        }

        /// <summary>
        /// Получает безопасный сервис БД (thread-safe singleton)
        /// </summary>
        private static SafePostgreSqlPrinterService GetDatabaseService()
        {
            if (!_useDatabaseProvider)
                return null;

            if (_printerService == null)
            {
                lock (_lockObject)
                {
                    if (_printerService == null)
                    {
                        try
                        {
                            _printerService = new SafePostgreSqlPrinterService(_connectionString);

                            // Инициализируем известные принтеры
                            List<PrinterControl> availablePrinters = GetPrinters();
                            string[] printerNames = availablePrinters.Select(p => p.PrinterName).ToArray();
                            _printerService.InitializePrinters(printerNames);

                            Log.Information("Database printer service initialized with {Count} printers", printerNames.Length);
                        }
                        catch (Exception ex)
                        {
                            Log.Error(ex, "Failed to initialize database service, falling back to XML: {Message}", ex.Message);
                            _printerService = null;
                        }
                    }
                }
            }
            return _printerService;
        }

        /// <summary>
        /// Попытка получить доступный принтер (гибридный подход)
        /// </summary>
        public static bool TryGetPrinter(string revitFilePath, out PrinterControl availablePrinter)
        {
            availablePrinter = null;

            // Сначала пробуем БД, если настроена
            if (_useDatabaseProvider && TryGetPrinterFromDatabase(revitFilePath, out availablePrinter))
            {
                return true;
            }

            // Fallback на XML если БД недоступна
            return TryGetPrinterFromXml(revitFilePath, out availablePrinter);
        }

        /// <summary>
        /// Получение принтера через безопасную БД
        /// </summary>
        private static bool TryGetPrinterFromDatabase(string revitFilePath, out PrinterControl availablePrinter)
        {
            availablePrinter = null;

            try
            {
                SafePostgreSqlPrinterService dbService = GetDatabaseService();
                if (dbService == null)
                    return false;

                // Очищаем зависшие блокировки
                CleanupExpiredReservationsInDatabase(dbService);

                List<PrinterControl> printerControls = GetPrinters();
                string[] preferredPrinters = printerControls
                    .Where(p => p.IsPrinterInstalled())
                    .Select(p => p.PrinterName)
                    .ToArray();

                if (preferredPrinters.Length == 0)
                {
                    Log.Warning("No installed printers found");
                    return false;
                }

                string userName = $"{Environment.UserName}@{Environment.MachineName}";
                string reservedPrinterName = dbService.TryReserveAnyAvailablePrinter(userName, preferredPrinters);

                if (!string.IsNullOrEmpty(reservedPrinterName))
                {
                    availablePrinter = printerControls.FirstOrDefault(p =>
                        string.Equals(p.PrinterName, reservedPrinterName, StringComparison.OrdinalIgnoreCase));

                    if (availablePrinter != null)
                    {
                        availablePrinter.RevitFilePath = revitFilePath;
                        Log.Information("Database: Reserved printer {PrinterName} for {UserName}",
                            reservedPrinterName, userName);
                        return true;
                    }
                }

                Log.Debug("Database: No available printers to reserve");
                return false;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Database printer reservation failed: {Message}", ex.Message);
                return false;
            }
        }

        /// <summary>
        /// Fallback получение принтера через XML (старый метод)
        /// </summary>
        private static bool TryGetPrinterFromXml(string revitFilePath, out PrinterControl availablePrinter)
        {
            int retryCount = 0;
            availablePrinter = null;
            const int maxRetries = 100;

            List<PrinterControl> printerList = GetPrinters();

            while (retryCount < maxRetries)
            {
                System.Threading.Thread.Sleep(maxRetries * retryCount++);

                Log.Debug("XML: Searching for an available printer (attempt {Attempt})", retryCount);

                foreach (PrinterControl printer in printerList)
                {
                    if (IsPrinterAvailable(printer))
                    {
                        Log.Debug("XML: Printer {PrinterName} is available", printer.PrinterName);
                        printer.RevitFilePath = revitFilePath;
                        availablePrinter = printer;
                        return true;
                    }
                }
            }

            Log.Warning("XML: No available printer found after {MaxRetries} attempts", maxRetries);
            return false;
        }

        /// <summary>
        /// Резервирует принтер (гибридный подход)
        /// </summary>
        public static void ReservePrinter(string printerName)
        {
            if (string.IsNullOrWhiteSpace(printerName))
                return;

            // Пробуем БД
            if (_useDatabaseProvider)
            {
                try
                {
                    SafePostgreSqlPrinterService dbService = GetDatabaseService();

                    if (dbService != null)
                    {
                        bool success = dbService.TryReserveSpecificPrinter(printerName, Environment.UserName);

                        if (success)
                        {
                            Log.Information("Database: Reserved printer {PrinterName}", printerName);
                            return;
                        }
                    }
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Database reserve failed for {PrinterName}: {Message}", printerName, ex.Message);
                }
            }

            // Fallback на XML
            ReservePrinterInXml(printerName);
        }

        /// <summary>
        /// Освобождает принтер (гибридный подход)
        /// </summary>
        public static void ReleasePrinter(string printerName)
        {
            if (string.IsNullOrWhiteSpace(printerName))
                return;

            // Пробуем БД
            if (_useDatabaseProvider)
            {
                try
                {
                    SafePostgreSqlPrinterService dbService = GetDatabaseService();
                    if (dbService != null)
                    {
                        string userName = $"{Environment.UserName}@{Environment.MachineName}";
                        bool success = dbService.ReleasePrinter(printerName, userName);

                        if (success)
                        {
                            Log.Information("Database: Released printer {PrinterName}", printerName);
                            return;
                        }
                    }
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Database release failed for {PrinterName}: {Message}", printerName, ex.Message);
                }
            }

            // Fallback на XML
            ReleasePrinterInXml(printerName);
        }

        /// <summary>
        /// Получить список принтеров в порядке приоритета
        /// </summary>
        public static List<PrinterControl> GetPrinters()
        {
            return new List<PrinterControl>
            {
                new BioPdfPrinter(),
                new Pdf24Printer(),
                new CreatorPrinter(),
                new ClawPdfPrinter(),
                new AdobePdfPrinter(),
                new InternalPrinter(),
            };
        }

        /// <summary>
        /// Проверяет доступность принтера (XML метод)
        /// </summary>
        public static bool IsPrinterAvailable(PrinterControl printer)
        {
            if (!printer.IsPrinterInstalled())
                return false;

            try
            {
                PrinterStateData states = LoadXmlState();
                PrinterInfo printerInfo = EnsurePrinterExists(states, printer.PrinterName);
                return printerInfo.IsAvailable;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "XML availability check failed for {PrinterName}: {Message}", printer.PrinterName, ex.Message);
                return false;
            }
        }

        #region XML Fallback Methods (существующая логика)

        private static readonly string docsPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        private static readonly string appDataFolder = Path.Combine(docsPath, "RevitBIMTool");
        private static readonly string stateFilePath = Path.Combine(appDataFolder, "PrinterState.xml");

        private static void ReservePrinterInXml(string printerName)
        {
            if (SetAvailabilityInXml(printerName, false))
            {
                Log.Debug("XML: Reserved printer {PrinterName}", printerName);
            }
        }

        private static void ReleasePrinterInXml(string printerName)
        {
            if (SetAvailabilityInXml(printerName, true))
            {
                Log.Debug("XML: Released printer {PrinterName}", printerName);
            }
        }

        private static bool SetAvailabilityInXml(string printerName, bool isAvailable)
        {
            try
            {
                PrinterStateData states = LoadXmlState();
                PrinterInfo printerInfo = EnsurePrinterExists(states, printerName);
                printerInfo.IsAvailable = isAvailable;

                return XmlHelper.SaveToXml(states, stateFilePath);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "XML state update failed for {PrinterName}: {Message}", printerName, ex.Message);
                return false;
            }
        }

        private static PrinterStateData LoadXmlState()
        {
            EnsureXmlStateFileExists();
            return XmlHelper.LoadFromXml<PrinterStateData>(stateFilePath) ?? CreateDefaultXmlState();
        }

        private static void EnsureXmlStateFileExists()
        {
            PathHelper.EnsureDirectory(appDataFolder);

            if (!File.Exists(stateFilePath))
            {
                PrinterStateData initialState = CreateDefaultXmlState();
                XmlHelper.SaveToXml(initialState, stateFilePath);
            }
        }

        private static PrinterStateData CreateDefaultXmlState()
        {
            PrinterStateData initialState = new PrinterStateData
            {
                LastUpdate = DateTime.Now,
                Printers = new List<PrinterInfo>()
            };

            foreach (PrinterControl printer in GetPrinters())
            {
                initialState.Printers.Add(new PrinterInfo(printer.PrinterName, true));
            }

            return initialState;
        }

        private static PrinterInfo EnsurePrinterExists(PrinterStateData states, string printerName, bool isAvailable = true)
        {
            PrinterInfo printerInfo = states.Printers?.Find(p => p.PrinterName == printerName);

            if (printerInfo == null)
            {
                printerInfo = new PrinterInfo(printerName, isAvailable);
                states.Printers.Add(printerInfo);

                if (!XmlHelper.SaveToXml(states, stateFilePath))
                {
                    throw new InvalidOperationException("Failed to save printer state");
                }
            }

            return printerInfo;
        }

        #endregion

        #region Database Cleanup

        private static void CleanupExpiredReservationsInDatabase(SafePostgreSqlPrinterService dbService)
        {
            try
            {
                TimeSpan maxAge = TimeSpan.FromMinutes(_lockTimeoutMinutes);
                int cleanedCount = dbService.CleanupExpiredReservations(maxAge);

                if (cleanedCount > 0)
                {
                    Log.Information("Database: Cleaned up {Count} expired reservations", cleanedCount);
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Database cleanup failed: {Message}", ex.Message);
            }
        }

        #endregion
    }
}