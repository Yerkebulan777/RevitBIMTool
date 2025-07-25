using Database.Configuration;
using Database.Services;
using System;

namespace Database.Extensions
{
    public static class DatabaseExtensions
    {
        /// <summary>
        /// Главная точка входа для инициализации системы принтеров
        /// </summary>
        public static IPrinterStateService InitializePrinterSystem(string connectionString)
        {
            // Инициализируем конфигурацию
            DatabaseConfig.Instance.Initialize(connectionString);

            // Создаем сервис
            PrinterStateService service = new();

            // Инициализируем стандартные принтеры
            string[] defaultPrinters = new[]
            {
                "PDF24",
                "PDF Writer - bioPDF",
                "PDFCreator",
                "clawPDF",
                "Adobe PDF"
            };

            service.InitializeSystem(defaultPrinters);

            return service;
        }

        /// <summary>
        /// Безопасное выполнение операции с принтером
        /// </summary>
        public static T WithPrinter<T>(this IPrinterStateService service, string[] preferredPrinters, Func<string, T> operation)
        {
            string reservedPrinter = null;
            string reservationId = $"{Environment.MachineName}_{DateTime.UtcNow:yyyyMMdd_HHmmss}";

            try
            {
                reservedPrinter = service.TryReserveAnyAvailablePrinter(reservationId, preferredPrinters);

                return string.IsNullOrEmpty(reservedPrinter)
                    ? throw new InvalidOperationException("No available printers found")
                    : operation(reservedPrinter);
            }
            finally
            {
                if (!string.IsNullOrEmpty(reservedPrinter))
                {
                    _ = service.ReleasePrinter(reservedPrinter);
                }
            }
        }
    }
}