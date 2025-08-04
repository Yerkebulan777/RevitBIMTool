using Dapper;
using Database.Logging;
using Database.Models;
using Database.Stores;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data;
using System.Data.Odbc;
using System.Diagnostics;
using System.Threading;

namespace Database.Services
{
    /// <summary>
    /// Thread-Safe Singleton для управления принтерами в многопроцессорной среде
    /// </summary>
    public sealed class PrinterManagerSingleton : IDisposable
    {
        private static readonly Lazy<PrinterManagerSingleton> _instance =
            new(() => new PrinterManagerSingleton(), LazyThreadSafetyMode.ExecutionAndPublication);

        public static PrinterManagerSingleton Instance => _instance.Value;

        private readonly string _connectionString;
        private readonly ILogger _logger;
        private readonly Timer _cleanupTimer;
        private readonly TimeSpan _stuckThreshold;
        private readonly ConcurrentDictionary<string, object> _printerLocks;
        private readonly int _commandTimeout;
        private bool _disposed;

        private PrinterManagerSingleton()
        {
            _connectionString = GetOptimizedConnectionString();
            _logger = LoggerFactory.CreateLogger<PrinterManagerSingleton>();
            _stuckThreshold = TimeSpan.FromMinutes(30);
            _printerLocks = new ConcurrentDictionary<string, object>();
            _commandTimeout = 30;

            // Автоматическая очистка каждые 5 минут
            _cleanupTimer = new Timer(
                CleanupStuckReservations,
                null,
                TimeSpan.FromMinutes(1),
                TimeSpan.FromMinutes(5));

            _logger.Information("PrinterManagerSingleton initialized with connection pooling");
        }

        /// <summary>
        /// Резервирует принтер с проверкой доступности
        /// </summary>
        public bool TryReservePrinter(string printerName, string revitFileName, out PrinterReservation reservation)
        {
            reservation = null;

            // Thread-safe блокировка на уровне принтера
            object printerLock = _printerLocks.GetOrAdd(printerName, _ => new object());

            lock (printerLock)
            {
                PrinterReservation tempReservation = null;

                bool success = ExecuteInSerializableTransaction(connection =>
                {
                    // Инициализируем принтер если не существует
                    InitializePrinter(connection, printerName);

                    // Проверяем доступность
                    if (!IsPrinterAvailable(connection, printerName))
                    {
                        _logger.Debug($"Printer {printerName} is not available");
                        return false;
                    }

                    // Создаем резервацию
                    tempReservation = new PrinterReservation
                    {
                        PrinterName = printerName,
                        RevitFileName = revitFileName,
                        ReservedAt = DateTime.UtcNow,
                        ProcessId = Process.GetCurrentProcess().Id,
                        SessionId = Guid.NewGuid(),
                        State = ReservationState.Reserved,
                        LastUpdate = DateTime.UtcNow
                    };

                    // Атомарно резервируем
                    int affected = connection.Execute(
                        PrinterSqlStore.ReservePrinterAtomic,
                        tempReservation,
                        commandTimeout: _commandTimeout);

                    if (affected > 0)
                    {
                        _logger.Information($"Reserved printer {printerName} for {revitFileName}");
                        return true;
                    }

                    tempReservation = null;
                    return false;
                });

                reservation = tempReservation;
                return success;
            }
        }

        /// <summary>
        /// Освобождает принтер
        /// </summary>
        public bool ReleasePrinter(string printerName, Guid sessionId, bool success = true)
        {
            object printerLock = _printerLocks.GetOrAdd(printerName, _ => new object());

            lock (printerLock)
            {
                return ExecuteInSerializableTransaction(connection =>
                {
                    ReservationState finalState = success ? ReservationState.Completed : ReservationState.Failed;

                    int affected = connection.Execute(
                        PrinterSqlStore.ReleasePrinterBySession,
                        new { printerName, sessionId, finalState = (int)finalState },
                        commandTimeout: _commandTimeout);

                    if (affected > 0)
                    {
                        _logger.Information($"Released printer {printerName} with status {finalState}");
                        return true;
                    }

                    return false;
                });
            }
        }

        /// <summary>
        /// Наглядная демонстрация поиска зависших резервации
        /// </summary>
        private void CleanupStuckReservations(object state)
        {
            try
            {
                _logger.Debug("=== НАЧАЛО ПОИСКА ЗАВИСШИХ РЕЗЕРВАЦИИ ===");

                List<PrinterReservation> stuckReservations = FindStuckReservations();

                _logger.Debug($"Найдено зависших резервации: {stuckReservations.Count}");

                foreach (PrinterReservation stuck in stuckReservations)
                {
                    _logger.Warning($"ЗАВИСШАЯ РЕЗЕРВАЦИЯ: " +
                        $"Принтер={stuck.PrinterName}, " +
                        $"Файл={stuck.RevitFileName}, " +
                        $"Процесс={stuck.ProcessId}, " +
                        $"Зависла={stuck.MinutesStuck:F1} мин");

                    // Проверяем, жив ли процесс
                    bool processAlive = IsProcessAlive(stuck.ProcessId);
                    _logger.Debug($"Процесс {stuck.ProcessId} живой: {processAlive}");

                    if (!processAlive)
                    {
                        CompensateStuckReservation(stuck);
                        _logger.Information($"Освобожден зависший принтер: {stuck.PrinterName}");
                    }
                    else
                    {
                        _logger.Information($"Процесс {stuck.ProcessId} еще работает, пропускаем");
                    }
                }

                _logger.Debug("=== КОНЕЦ ПОИСКА ЗАВИСШИХ РЕЗЕРВАЦИИ ===");
            }
            catch (Exception ex)
            {
                _logger.Error("Ошибка при очистке зависших резервации", ex);
            }
        }

        /// <summary>
        /// Находит все зависшие резервации с детальной информацией
        /// </summary>
        private List<PrinterReservation> FindStuckReservations()
        {
            return ExecuteInSerializableTransaction(connection =>
            {
                DateTime cutoffTime = DateTime.UtcNow.Subtract(_stuckThreshold);

                _logger.Debug($"Ищем резервации старше: {cutoffTime:yyyy-MM-dd HH:mm:ss}");

                List<PrinterReservation> stuck = [.. connection.Query<PrinterReservation>(PrinterSqlStore.FindStuckReservations, new { cutoffTime }, commandTimeout: _commandTimeout)];

                _logger.Debug($"SQL вернул {stuck.Count} зависших резервации");

                return stuck;
            });
        }

        /// <summary>
        /// Компенсирует зависшую резервацию
        /// </summary>
        private void CompensateStuckReservation(PrinterReservation reservation)
        {
            _ = ExecuteInSerializableTransaction(connection =>
            {
                _logger.Warning($"Компенсируем зависшую резервацию для {reservation.PrinterName}");

                // Освобождаем принтер
                int affected = connection.Execute(
                    PrinterSqlStore.CompensateStuckReservation,
                    new { reservation.PrinterName, reservation.SessionId },
                    commandTimeout: _commandTimeout);

                if (affected > 0)
                {
                    // Логируем компенсацию для аудита
                    _ = connection.Execute(
                        PrinterSqlStore.LogCompensation,
                        new
                        {
                            reservation.PrinterName,
                            reservation.RevitFileName,
                            reservation.SessionId,
                            reason = $"Процесс {reservation.ProcessId} не отвечает {reservation.MinutesStuck:F1} мин"
                        },
                        commandTimeout: _commandTimeout);

                    _logger.Information($"Компенсация выполнена для {reservation.PrinterName}");
                }

                return affected > 0;
            });
        }

        private void InitializePrinter(OdbcConnection connection, string printerName)
        {
            _ = connection.Execute(
                PrinterSqlStore.InitializePrinter,
                new { printerName },
                commandTimeout: _commandTimeout);
        }

        private bool IsPrinterAvailable(OdbcConnection connection, string printerName)
        {
            return connection.QuerySingleOrDefault<bool>(
                PrinterSqlStore.IsPrinterAvailable,
                new { printerName },
                commandTimeout: _commandTimeout);
        }

        private bool IsProcessAlive(int? processId)
        {
            if (!processId.HasValue)
            {
                return false;
            }

            try
            {
                Process process = Process.GetProcessById(processId.Value);
                return !process.HasExited;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Выполняет операцию в транзакции с уровнем изоляции SERIALIZABLE
        /// </summary>
        private T ExecuteInSerializableTransaction<T>(Func<OdbcConnection, T> operation)
        {
            const int maxRetries = 3;

            for (int attempt = 1; attempt <= maxRetries; attempt++)
            {
                try
                {
                    using OdbcConnection connection = new(_connectionString);
                    connection.Open();

                    using OdbcTransaction transaction = connection.BeginTransaction(IsolationLevel.Serializable);
                    try
                    {
                        T result = operation(connection);
                        transaction.Commit();
                        return result;
                    }
                    catch
                    {
                        transaction.Rollback();
                        throw;
                    }
                }
                catch (OdbcException ex) when (IsRetryableError(ex) && attempt < maxRetries)
                {
                    Thread.Sleep(100 * attempt); // Exponential backoff
                    _logger.Debug($"Retry attempt {attempt} due to: {ex.Message}");
                }
            }

            throw new InvalidOperationException($"Transaction failed after {maxRetries} attempts");
        }

        private static bool IsRetryableError(OdbcException ex)
        {
            string message = ex.Message.ToLowerInvariant();
            return message.Contains("serialization failure") ||
                   message.Contains("deadlock detected") ||
                   message.Contains("could not serialize access");
        }

        /// <summary>
        /// Оптимизированная строка подключения с connection pooling
        /// </summary>
        private static string GetOptimizedConnectionString()
        {
            string baseConnection = System.Configuration.ConfigurationManager
                .ConnectionStrings["PrinterDatabase"]?.ConnectionString;

            if (string.IsNullOrEmpty(baseConnection))
            {
                throw new InvalidOperationException("Connection string not found");
            }

            // Добавляем оптимизации для PostgreSQL через ODBC
            if (!baseConnection.Contains("MaxPoolSize"))
            {
                baseConnection += ";MaxPoolSize=20;MinPoolSize=5;ConnectionLifetime=300;";
            }

            if (!baseConnection.Contains("ConnSettings"))
            {
                baseConnection += "ConnSettings=" +
                    "SET statement_timeout=30000;" +
                    "SET lock_timeout=5000;" +
                    "SET default_transaction_isolation='serializable';" +
                    "SET tcp_keepalives_idle=300;" +
                    "SET tcp_keepalives_interval=30;";
            }

            return baseConnection;
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _cleanupTimer?.Dispose();
                _disposed = true;
                _logger.Information("PrinterManagerSingleton disposed");
            }
        }
    }
}