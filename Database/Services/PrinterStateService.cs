// Database/Services/PrinterStateService.cs
using Database.Configuration;
using Database.Models;
using Database.Repositories;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Database.Services
{
    /// <summary>
    /// Основной сервис управления состоянием принтеров
    /// Реализует паттерн Unit of Work для управления транзакциями
    /// Обеспечивает retry-логику для обработки временных сбоев
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
        /// Использует приоритетный список принтеров для оптимального распределения
        /// Весь процесс выполняется в одной транзакции для консистентности
        /// </summary>
        public string TryReserveAnyAvailablePrinter(string reservedBy, IEnumerable<string> preferredPrinters = null)
        {
            return ExecuteWithRetry(() =>
            {
                using (var connection = new NpgsqlConnection(_config.ConnectionString))
                {
                    connection.Open();

                    using (var transaction = connection.BeginTransaction())
                    {
                        try
                        {
                            // Получаем список доступных принтеров
                            var availablePrinters = _repository.GetAvailablePrinters(transaction).ToList();

                            if (!availablePrinters.Any())
                                return null;

                            // Упорядочиваем принтеры по приоритету
                            var orderedPrinters = OrderPrintersByPriority(availablePrinters, preferredPrinters);

                            // Пытаемся зарезервировать первый доступный
                            foreach (var printer in orderedPrinters)
                            {
                                if (_repository.TryReservePrinter(printer.PrinterName, reservedBy, transaction))
                                {
                                    transaction.Commit();
                                    return printer.PrinterName;
                                }
                            }

                            transaction.Rollback();
                            return null;
                        }
                        catch
                        {
                            transaction.Rollback();
                            throw;
                        }
                    }
                }
            });
        }

        /// <summary>
        /// Резервирование конкретного принтера с retry-логикой
        /// Обрабатывает временные сбои базы данных
        /// </summary>
        public bool TryReserveSpecificPrinter(string printerName, string reservedBy)
        {
            return ExecuteWithRetry(() =>
            {
                using (var connection = new NpgsqlConnection(_config.ConnectionString))
                {
                    connection.Open();

                    using (var transaction = connection.BeginTransaction())
                    {
                        try
                        {
                            bool success = _repository.TryReservePrinter(printerName, reservedBy, transaction);

                            if (success)
                                transaction.Commit();
                            else
                                transaction.Rollback();

                            return success;
                        }
                        catch
                        {
                            transaction.Rollback();
                            throw;
                        }
                    }
                }
            });
        }

        /// <summary>
        /// Освобождение принтера - простая операция без retry
        /// Ошибки освобождения не критичны для системы
        /// </summary>
        public bool ReleasePrinter(string printerName)
        {
            try
            {
                return _repository.ReleasePrinter(printerName);
            }
            catch (Exception)
            {
                // Логируем ошибку, но не пробрасываем - освобождение некритично
                return false;
            }
        }

        /// <summary>
        /// Получение полного состояния системы принтеров
        /// </summary>
        public IEnumerable<PrinterState> GetAllPrinters()
        {
            using (var connection = new NpgsqlConnection(_config.ConnectionString))
            {
                connection.Open();

                const string sql = @"
                    SELECT id, printer_name, is_available, reserved_by, reserved_at, 
                           last_updated, process_id, machine_name, version
                    FROM printer_states 
                    ORDER BY printer_name";

                var results = new List<PrinterState>();

                using (var command = new NpgsqlCommand(sql, connection))
                {
                    command.CommandTimeout = _config.CommandTimeout;

                    using (var reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            results.Add(new PrinterState
                            {
                                Id = reader.GetInt32("id"),
                                PrinterName = reader.GetString("printer_name"),
                                IsAvailable = reader.GetBoolean("is_available"),
                                ReservedBy = reader.IsDBNull("reserved_by") ? null : reader.GetString("reserved_by"),
                                ReservedAt = reader.IsDBNull("reserved_at") ? (DateTime?)null : reader.GetDateTime("reserved_at"),
                                LastUpdated = reader.GetDateTime("last_updated"),
                                ProcessId = reader.IsDBNull("process_id") ? (int?)null : reader.GetInt32("process_id"),
                                MachineName = reader.IsDBNull("machine_name") ? null : reader.GetString("machine_name"),
                                Version = reader.GetInt64("version")
                            });
                        }
                    }
                }

                return results;
            }
        }

        /// <summary>
        /// Инициализация системы принтеров при первом запуске
        /// Создает базовые записи для всех известных принтеров
        /// </summary>
        public void InitializeSystem(IEnumerable<string> printerNames)
        {
            ExecuteWithRetry(() =>
            {
                _repository.InitializePrinters(printerNames);
                return true;
            });
        }

        /// <summary>
        /// Периодическая очистка зависших резервирований
        /// Должна вызываться по расписанию для поддержания системы в рабочем состоянии
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
            var printer = _repository.GetByName(printerName);
            return printer?.IsAvailable == true;
        }

        /// <summary>
        /// Алгоритм упорядочивания принтеров по приоритету
        /// Предпочтительные принтеры идут первыми, остальные - по алфавиту
        /// </summary>
        private IEnumerable<PrinterState> OrderPrintersByPriority(
            IEnumerable<PrinterState> availablePrinters,
            IEnumerable<string> preferredPrinters)
        {
            if (preferredPrinters == null)
                return availablePrinters.OrderBy(p => p.PrinterName);

            var preferredSet = new HashSet<string>(preferredPrinters, StringComparer.OrdinalIgnoreCase);

            return availablePrinters
                .OrderBy(p => preferredSet.Contains(p.PrinterName) ? 0 : 1)
                .ThenBy(p => p.PrinterName);
        }

        /// <summary>
        /// Универсальная retry-логика для обработки временных сбоев
        /// Использует экспоненциальную задержку между попытками
        /// Обрабатывает специфичные для PostgreSQL исключения
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
                catch (NpgsqlException ex) when (IsTransientError(ex))
                {
                    lastException = ex;

                    if (attempt < _config.MaxRetryAttempts - 1)
                    {
                        // Экспоненциальная задержка: 100ms, 400ms, 1600ms
                        var delay = TimeSpan.FromMilliseconds(100 * Math.Pow(4, attempt));
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


        private bool IsTransientError(NpgsqlException ex)
        {
            switch (ex.SqlState)
            {
                case "40001": // serialization_failure
                case "40P01": // deadlock_detected  
                case "53300": // too_many_connections
                case "08000": // connection_exception
                case "08003": // connection_does_not_exist
                case "08006": // connection_failure
                    return true;
                default:
                    return false;
            }
        }
    }
}