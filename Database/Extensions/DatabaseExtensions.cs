using Database.Configuration;
using Database.Services;
using System;
using System.Diagnostics;

namespace Database.Extensions
{
    /// <summary>
    /// Методы расширения для упрощения интеграции с основным приложением
    /// </summary>
    public static class DatabaseExtensions
    {
        /// <summary>
        /// Инициализация системы управления принтерами
        /// Этот метод - главная точка входа для использования в вашем RevitBIMTool
        /// </summary>
        public static IPrinterStateService InitializePrinterSystem(string connectionString = null)
        {
            // Инициализируем конфигурацию - она автоматически определит тип СУБД
            DatabaseConfig.Instance.Initialize(connectionString);

            // Создаем сервис
            PrinterStateService service = new();

            // Инициализируем стандартные принтеры из вашего кода
            string[] defaultPrinters = new[]
            {
                "PDF Writer - bioPDF",
                "PDF24",
                "PDFCreator",
                "clawPDF",
                "Adobe PDF"
            };

            service.InitializeSystem(defaultPrinters);

            return service;
        }

        /// <summary>
        /// Создание уникального идентификатора резервирования
        /// </summary>
        public static string CreateReservationId()
        {
            Process process = Process.GetCurrentProcess();
            return $"{Environment.MachineName}_{process.Id}_{DateTime.UtcNow:yyyyMMdd_HHmmss}";
        }

        /// <summary>
        /// Безопасное выполнение операции с принтером
        /// Паттерн "использование ресурса" - автоматически освобождает принтер
        /// </summary>
        public static T WithPrinter<T>(this IPrinterStateService service, string[] preferredPrinters, Func<string, T> operation)
        {
            string reservedPrinter = null;
            string reservationId = CreateReservationId();

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