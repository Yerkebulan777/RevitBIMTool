using Database.Models;
using System;
using System.Collections.Generic;

namespace Database.Services
{
    /// <summary>
    /// Интерфейс высокоуровневого сервиса управления принтерами
    /// Предоставляет бизнес-логику работы с принтерами
    /// </summary>
    public interface IPrinterStateService
    {
        /// <summary>
        /// Попытаться зарезервировать доступный принтер
        /// Возвращает имя зарезервированного принтера или null
        /// </summary>
        string TryReserveAnyAvailablePrinter(string reservedBy, IEnumerable<string> preferredPrinters = null);

        /// <summary>
        /// Зарезервировать конкретный принтер
        /// </summary>
        bool TryReserveSpecificPrinter(string printerName, string reservedBy);

        /// <summary>
        /// Освободить принтер
        /// </summary>
        bool ReleasePrinter(string printerName);

        /// <summary>
        /// Получить состояние всех принтеров
        /// </summary>
        IEnumerable<PrinterState> GetAllPrinters();

        /// <summary>
        /// Инициализировать систему принтеров
        /// </summary>
        void InitializeSystem(IEnumerable<string> printerNames);

        /// <summary>
        /// Очистить зависшие резервирования
        /// </summary>
        int CleanupExpiredReservations(TimeSpan maxAge);

        /// <summary>
        /// Проверить доступность принтера
        /// </summary>
        bool IsPrinterAvailable(string printerName);
    }
}