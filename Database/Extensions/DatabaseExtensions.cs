using System;
using System.Diagnostics;
using Database.Configuration;
using Database.Services;

namespace Database.Extensions
{
    /// <summary>
    /// Методы расширения для упрощения интеграции с основным приложением
    /// Предоставляет удобные фасады для типичных операций
    /// </summary>
    public static class DatabaseExtensions
    {
        /// <summary>
        /// Инициализация системы управления принтерами
        /// Настраивает подключение и создает необходимые структуры
        /// </summary>
        public static IPrinterStateService InitializePrinterSystem(string connectionString = null)
        {
            // Инициализируем конфигурацию
            DatabaseConfig.Instance.Initialize(connectionString);

            // Создаем сервис
            var service = new PrinterStateService();

            // Инициализируем стандартные принтеры
            var defaultPrinters = new[]
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
        /// Создание идентификатора резервирования на основе текущего процесса
        /// Обеспечивает уникальность резервирований
        /// </summary>
        public static string CreateReservationId()
        {
            var process = Process.GetCurrentProcess();
            return $"{Environment.MachineName}_{process.Id}_{DateTime.UtcNow:yyyyMMdd_HHmmss}";
        }

        /// <summary>
        /// Безопасное выполнение операции с принтером
        /// Автоматически освобождает принтер при ошибке
        /// </summary>
        public static T WithPrinter<T>(this IPrinterStateService service,
            string[] preferredPrinters,
            Func<string, T> operation)
        {
            string reservedPrinter = null;
            string reservationId = CreateReservationId();

            try
            {
                reservedPrinter = service.TryReserveAnyAvailablePrinter(reservationId, preferredPrinters);

                if (string.IsNullOrEmpty(reservedPrinter))
                    throw new InvalidOperationException("No available printers found");

                return operation(reservedPrinter);
            }
            finally
            {
                if (!string.IsNullOrEmpty(reservedPrinter))
                {
                    service.ReleasePrinter(reservedPrinter);
                }
            }
        }
    }
}