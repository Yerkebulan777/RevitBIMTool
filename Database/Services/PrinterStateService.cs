using Database.Configuration;
using Database.Models;
using Database.Repositories;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;

namespace Database.Services
{
    /// <summary>
    /// Сервис управления состоянием принтеров
    /// Полностью независим от конкретной СУБД, работает через абстракции ADO.NET
    /// Реализует паттерн Unit of Work для правильного управления транзакциями
    /// </summary>
    public class PrinterStateService : IPrinterStateService
    {
        private readonly IPrinterRepository _repository;
        private readonly DatabaseConfig _config;

        public PrinterStateService(IPrinterRepository repository = null)
        {
            _repository = repository ?? new PrinterRepository();
            _config = DatabaseConfig.Instance;
        }

        /// <summary>
        /// Алгоритм поиска и резервирования доступного принтера
        /// Ключевая особенность: весь процесс выполняется в одной транзакции
        /// Это гарантирует, что либо резервирование произойдет полностью,
        /// либо не произойдет вовсе (принцип атомарности)
        /// </summary>
        public string TryReserveAnyAvailablePrinter(string reservedBy, IEnumerable<string> preferredPrinters = null)
        {
            return ExecuteWithRetry(() =>
            {
                // Создаем подключение через провайдер - он знает, какую СУБД использовать
                using IDbConnection connection = _config.Provider.CreateConnection(_config.ConnectionString);
                connection.Open();

                using IDbTransaction transaction = connection.BeginTransaction();
                try
                {
                    // Этап 1: Получаем список всех доступных принтеров
                    List<PrinterState> availablePrinters = _repository.GetAvailablePrinters(transaction).ToList();

                    if (!availablePrinters.Any())
                    {
                        return null; // Нет доступных принтеров
                    }

                    // Этап 2: Упорядочиваем принтеры по приоритету
                    // Предпочтительные принтеры пробуем резервировать первыми
                    IEnumerable<PrinterState> orderedPrinters = OrderPrintersByPriority(availablePrinters, preferredPrinters);

                    // Этап 3: Пытаемся зарезервировать первый доступный принтер
                    foreach (PrinterState printer in orderedPrinters)
                    {
                        if (_repository.TryReservePrinter(printer.PrinterName, reservedBy, transaction))
                        {
                            // Успешно зарезервировали - коммитим транзакцию
                            transaction.Commit();
                            return printer.PrinterName;
                        }
                    }

                    // Не удалось зарезервировать ни один принтер - откатываем
                    transaction.Rollback();
                    return null;
                }
                catch
                {
                    // При любой ошибке откатываем транзакцию
                    transaction.Rollback();
                    throw;
                }
            });
        }

        /// <summary>
        /// Резервирование конкретного принтера
        /// Более простая операция - работаем только с одним принтером
        /// </summary>
        public bool TryReserveSpecificPrinter(string printerName, string reservedBy)
        {
            return ExecuteWithRetry(() =>
            {
                using IDbConnection connection = _config.Provider.CreateConnection(_config.ConnectionString);
                connection.Open();

                using IDbTransaction transaction = connection.BeginTransaction();
                try
                {
                    bool success = _repository.TryReservePrinter(printerName, reservedBy, transaction);

                    if (success)
                    {
                        transaction.Commit();
                    }
                    else
                    {
                        transaction.Rollback();
                    }

                    return success;
                }
                catch
                {
                    transaction.Rollback();
                    throw;
                }
            });
        }

        /// <summary>
        /// Освобождение принтера
        /// Простая операция - не требует retry логики, так как ошибки освобождения не критичны
        /// </summary>
        public bool ReleasePrinter(string printerName)
        {
            try
            {
                return _repository.ReleasePrinter(printerName);
            }
            catch (Exception)
            {
                // Логируем ошибку, но не пробрасываем - освобождение некритично для работы системы
                // В реальном приложении здесь был бы вызов logger.LogError(ex, ...)
                return false;
            }
        }

        /// <summary>
        /// Получение полного состояния системы принтеров
        /// Используется для мониторинга и диагностики
        /// </summary>
        public IEnumerable<PrinterState> GetAllPrinters()
        {
            using IDbConnection connection = _config.Provider.CreateConnection(_config.ConnectionString);
            connection.Open();

            const string sql = @"
                    SELECT id, printer_name, is_available, reserved_by, reserved_at, 
                           last_updated, process_id, machine_name, version
                    FROM printer_states 
                    ORDER BY printer_name";

            List<PrinterState> results = new();

            using (IDbCommand command = connection.CreateCommand())
            {
                command.CommandText = sql;
                command.CommandTimeout = _config.CommandTimeout;

                using IDataReader reader = command.ExecuteReader();
                while (reader.Read())
                {
                    results.Add(new PrinterState
                    {
                        Id = Convert.ToInt32(reader["id"]),
                        PrinterName = reader["printer_name"].ToString(),
                        IsAvailable = Convert.ToBoolean(reader["is_available"]),
                        ReservedBy = reader["reserved_by"] as string,
                        ReservedAt = ParseDateTime(reader["reserved_at"]),
                        LastUpdated = ParseDateTime(reader["last_updated"]) ?? DateTime.UtcNow,
                        ProcessId = reader["process_id"] as int?,
                        MachineName = reader["machine_name"] as string,
                        Version = Convert.ToInt64(reader["version"])
                    });
                }
            }

            return results;
        }

        /// <summary>
        /// Инициализация системы принтеров при первом запуске
        /// </summary>
        public void InitializeSystem(IEnumerable<string> printerNames)
        {
            _ = ExecuteWithRetry(() =>
            {
                _repository.InitializePrinters(printerNames);
                return true;
            });
        }

        /// <summary>
        /// Периодическая очистка зависших резервирований
        /// Важная функция для поддержания системы в рабочем состоянии
        /// </summary>
        public int CleanupExpiredReservations(TimeSpan maxAge)
        {
            return ExecuteWithRetry(() => _repository.CleanupExpiredReservations(maxAge));
        }

        /// <summary>
        /// Быстрая проверка доступности принтера
        /// </summary>
        public bool IsPrinterAvailable(string printerName)
        {
            PrinterState printer = _repository.GetByName(printerName);
            return printer?.IsAvailable == true;
        }

        #region Вспомогательные методы

        /// <summary>
        /// Алгоритм упорядочивания принтеров по приоритету
        /// Предпочтительные принтеры идут первыми, остальные - по алфавиту
        /// Это помогает равномерно распределять нагрузку между принтерами
        /// </summary>
        private IEnumerable<PrinterState> OrderPrintersByPriority(
            IEnumerable<PrinterState> availablePrinters,
            IEnumerable<string> preferredPrinters)
        {
            if (preferredPrinters == null)
            {
                return availablePrinters.OrderBy(p => p.PrinterName);
            }

            HashSet<string> preferredSet = new(preferredPrinters, StringComparer.OrdinalIgnoreCase);

            return availablePrinters
                .OrderBy(p => preferredSet.Contains(p.PrinterName) ? 0 : 1) // Предпочтительные первыми
                .ThenBy(p => p.PrinterName); // Затем по алфавиту
        }

        /// <summary>
        /// Универсальная retry-логика для обработки временных сбоев
        /// Обрабатывает блокировки, таймауты подключения и другие временные проблемы
        /// Использует экспоненциальную задержку между попытками
        /// </summary>
        private T ExecuteWithRetry<T>(Func<T> operation)
        {
            Exception lastException = null;

            for (int attempt = 0; attempt < _config.MaxRetryAttempts; attempt++)
            {
                try
                {
                    return operation();
                }
                catch (Exception ex) when (IsTransientError(ex))
                {
                    lastException = ex;

                    if (attempt < _config.MaxRetryAttempts - 1)
                    {
                        // Экспоненциальная задержка: 100ms, 400ms, 1600ms
                        TimeSpan delay = TimeSpan.FromMilliseconds(100 * Math.Pow(4, attempt));
                        System.Threading.Thread.Sleep(delay);
                    }
                }
                catch (Exception ex)
                {
                    // Не временная ошибка - сразу пробрасываем
                    throw new InvalidOperationException($"Database operation failed: {ex.Message}", ex);
                }
            }

            throw new InvalidOperationException(
                $"Operation failed after {_config.MaxRetryAttempts} attempts", lastException);
        }

        /// <summary>
        /// Определение временных ошибок, которые стоит повторить
        /// Разные СУБД возвращают разные типы исключений для временных проблем
        /// </summary>
        private bool IsTransientError(Exception ex)
        {
            // Общие признаки временных ошибок для всех СУБД
            string message = ex.Message.ToLowerInvariant();

            return message.Contains("timeout") ||
                   message.Contains("deadlock") ||
                   message.Contains("connection") ||
                   message.Contains("network") ||
                   message.Contains("busy");
        }

        /// <summary>
        /// Вспомогательный метод для парсинга дат
        /// </summary>
        private DateTime? ParseDateTime(object value)
        {
            if (value == null || value == DBNull.Value)
            {
                return null;
            }

            if (value is DateTime dateTime)
            {
                return dateTime;
            }

            return value is string dateString && DateTime.TryParse(dateString, out DateTime parsed) ? parsed : (DateTime?)null;
        }

        #endregion
    }
}