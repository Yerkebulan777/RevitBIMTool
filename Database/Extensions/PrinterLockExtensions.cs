
using Database.Models;
using Database.Services;
using System;

namespace Database.Extensions
{
    /// <summary>
    /// Упрощенный API для работы с блокировками принтеров
    /// </summary>
    public static class PrinterLockExtensions
    {
        /// <summary>
        /// Выполнить действие с заблокированным принтером
        /// Автоматически освобождает блокировку в конце
        /// </summary>
        public static T WithPrinterLock<T>(this DistributedPrinterLockService lockService,
            string printerName, Func<string, T> action, TimeSpan? lockDuration = null)
        {
            PrinterLock printerLock = lockService.TryAcquireLock(printerName, duration: lockDuration);

            if (printerLock == null)
            {
                throw new InvalidOperationException($"Cannot acquire lock for printer: {printerName}");
            }

            try
            {
                return action(printerName);
            }
            finally
            {
                _ = lockService.ReleaseLock(printerLock.LockId);
            }
        }

        /// <summary>
        /// Попытка выполнить действие с принтером
        /// Возвращает false если не удалось получить блокировку
        /// </summary>
        public static bool TryWithPrinterLock<T>(this DistributedPrinterLockService lockService,
            string printerName, Func<string, T> action, out T result, TimeSpan? lockDuration = null)
        {
            result = default;
            Models.PrinterLock printerLock = lockService.TryAcquireLock(printerName, duration: lockDuration);

            if (printerLock == null)
            {
                return false;
            }

            try
            {
                result = action(printerName);
                return true;
            }
            finally
            {
                _ = lockService.ReleaseLock(printerLock.LockId);
            }
        }
    }
}
