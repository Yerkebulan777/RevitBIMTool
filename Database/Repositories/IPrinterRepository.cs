
using Database.Models;
using System;
using System.Collections.Generic;
using System.Data;

namespace Database.Repositories
{
    /// <summary>
    /// Интерфейс репозитория для работы с состоянием принтеров
    /// Определяет контракт для всех операций с принтерами
    /// </summary>
    public interface IPrinterRepository
    {
        /// <summary>
        /// Получить состояние принтера по имени
        /// </summary>
        PrinterState GetByName(string printerName, IDbTransaction transaction = null);

        /// <summary>
        /// Получить все доступные принтеры
        /// </summary>
        IEnumerable<PrinterState> GetAvailablePrinters(IDbTransaction transaction = null);

        /// <summary>
        /// Создать или обновить состояние принтера
        /// </summary>
        bool UpsertPrinter(PrinterState printerState, IDbTransaction transaction = null);

        /// <summary>
        /// Атомарно зарезервировать принтер
        /// Использует SELECT FOR UPDATE для предотвращения race conditions
        /// </summary>
        bool TryReservePrinter(string printerName, string reservedBy, IDbTransaction transaction = null);

        /// <summary>
        /// Освободить принтер
        /// </summary>
        bool ReleasePrinter(string printerName, IDbTransaction transaction = null);

        /// <summary>
        /// Очистить зависшие резервации (автоматическое освобождение)
        /// </summary>
        int CleanupExpiredReservations(TimeSpan expiredAfter, IDbTransaction transaction = null);

        /// <summary>
        /// Инициализировать принтеры в базе данных
        /// </summary>
        void InitializePrinters(IEnumerable<string> printerNames, IDbTransaction transaction = null);
    }
}