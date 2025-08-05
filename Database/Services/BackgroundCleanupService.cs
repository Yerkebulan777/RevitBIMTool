using CommonUtils;
using Dapper;
using Database.Models;
using Database.Stores;
using Serilog;
using System.Data.Odbc;
using System.Diagnostics;

namespace Database.Services
{
    public sealed class BackgroundCleanupService : IDisposable
    {
        private readonly string _connectionString;
        private readonly ILogger _logger;
        private readonly Timer _cleanupTimer;
        private readonly TimeSpan _stuckThreshold;
        private readonly int _commandTimeout;
        private bool _disposed;

        public BackgroundCleanupService(string connectionString, TimeSpan stuckThreshold, int commandTimeout = 30)
        {
            //_logger = LoggerFactory.CreateLogger<BackgroundCleanupService>();
            _connectionString = connectionString;
            _stuckThreshold = stuckThreshold;
            _commandTimeout = commandTimeout;

            // Запуск очистки каждые 5 минут
            _cleanupTimer = new Timer(
                CleanupStuckReservations,
                null,
                TimeSpan.FromMinutes(1),
                TimeSpan.FromMinutes(5));

            _logger.Information("BackgroundCleanupService initialized");
        }

        /// <summary>
        /// Основной метод очистки зависших резервации
        /// </summary>
        private void CleanupStuckReservations(object state)
        {
            try
            {
                _logger.Debug("=== НАЧАЛО ФОНОВОЙ ОЧИСТКИ ЗАВИСШИХ РЕЗЕРВАЦИИ ===");

                List<PrinterReservation> stuckReservations = FindStuckReservations();

                if (!stuckReservations.Any())
                {
                    _logger.Debug("Зависших резервации не найдено");
                    return;
                }

                _logger.Warning($"Найдено {stuckReservations.Count} зависших резервации:");

                foreach (PrinterReservation stuck in stuckReservations)
                {
                    _logger.Warning($"ЗАВИСШАЯ РЕЗЕРВАЦИЯ: " +
                        $"Принтер='{stuck.PrinterName}', " +
                        $"Файл='{stuck.RevitFileName}', " +
                        $"Процесс={stuck.ProcessId}, " +
                        $"Зависла {stuck.MinutesStuck:F1} мин, " +
                        $"Статус={stuck.State}");

                    // Проверяем жизнеспособность процесса
                    bool processAlive = IsProcessAlive(stuck.ProcessId);
                    _logger.Debug($"Процесс {stuck.ProcessId} активен: {processAlive}");

                    if (!processAlive)
                    {
                        CompensateStuckReservation(stuck);
                        _logger.Information($"✓ Освобожден зависший принтер: {stuck.PrinterName}");
                    }
                    else
                    {
                        _logger.Information($"→ Процесс {stuck.ProcessId} еще работает, пропускаем");
                    }
                }

                _logger.Debug("=== КОНЕЦ ФОНОВОЙ ОЧИСТКИ ===");
            }
            catch (Exception ex)
            {
                _logger.Error($"Критическая ошибка при фоновой очистке: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Поиск всех зависших резервации используя PrinterReservation
        /// </summary>
        private List<PrinterReservation> FindStuckReservations()
        {
            try
            {
                using var connection = new OdbcConnection(_connectionString);
                connection.Open();

                DateTime cutoffTime = DateTime.UtcNow.Subtract(_stuckThreshold);

                _logger.Debug($"Поиск резервации старше: {cutoffTime:yyyy-MM-dd HH:mm:ss} UTC");

                // Используем существующий SQL запрос из PrinterSqlStore
                List<PrinterReservation> stuckReservations = connection
                    .Query<PrinterReservation>(
                        PrinterSqlStore.FindStuckReservations,
                        new { cutoffTime },
                        commandTimeout: _commandTimeout)
                    .ToList();

                _logger.Debug($"SQL запрос вернул {stuckReservations.Count} потенциально зависших резервации");

                // Дополнительная фильтрация по пороговому времени
                var filteredReservations = stuckReservations
                    .Where(r => r.IsStuck(_stuckThreshold))
                    .ToList();

                _logger.Debug($"После фильтрации осталось {filteredReservations.Count} зависших резервации");

                return filteredReservations;
            }
            catch (Exception ex)
            {
                _logger.Error($"Ошибка при поиске зависших резервации: {ex.Message}", ex);
                return new List<PrinterReservation>();
            }
        }

        /// <summary>
        /// Компенсация конкретной зависшей резервации
        /// </summary>
        private void CompensateStuckReservation(PrinterReservation reservation)
        {
            try
            {
                using var connection = new OdbcConnection(_connectionString);
                connection.Open();

                using var transaction = connection.BeginTransaction();
                try
                {
                    _logger.Information($"Компенсируем зависшую резервацию: {reservation.PrinterName}");

                    // Освобождаем принтер
                    int printerReleased = connection.Execute(
                        PrinterSqlStore.CompensateStuckReservation,
                        new { reservation.PrinterName, reservation.SessionId },
                        transaction,
                        commandTimeout: _commandTimeout);

                    if (printerReleased > 0)
                    {
                        // Логируем компенсацию для аудита
                        string reason = $"Фоновая очистка: процесс {reservation.ProcessId} " +
                                       $"не отвечает {reservation.MinutesStuck:F1} минут";

                        int loggedCompensation = connection.Execute(
                            PrinterSqlStore.LogCompensation,
                            new
                            {
                                reservation.PrinterName,
                                reservation.RevitFileName,
                                reservation.SessionId,
                                reason
                            },
                            transaction,
                            commandTimeout: _commandTimeout);

                        transaction.Commit();

                        _logger.Information($"✓ Компенсация выполнена успешно для принтера '{reservation.PrinterName}'. " +
                                          $"Обновлено записей: {printerReleased}, логов: {loggedCompensation}");
                    }
                    else
                    {
                        transaction.Rollback();
                        _logger.Warning($"⚠️ Не удалось освободить принтер '{reservation.PrinterName}' - " +
                                       "возможно, он уже был освобожден другим процессом");
                    }
                }
                catch
                {
                    transaction.Rollback();
                    throw;
                }
            }
            catch (Exception ex)
            {
                _logger.Error($"Ошибка при компенсации резервации принтера '{reservation.PrinterName}': {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Проверка активности процесса
        /// </summary>
        private bool IsProcessAlive(int? processId)
        {
            if (!processId.HasValue)
            {
                _logger.Debug("ProcessId отсутствует - считаем процесс неактивным");
                return false;
            }

            try
            {
                using Process process = Process.GetProcessById(processId.Value);
                bool isAlive = !process.HasExited;
                _logger.Debug($"Процесс {processId.Value}: alive={isAlive}, name='{process.ProcessName}'");
                return isAlive;
            }
            catch (ArgumentException)
            {
                _logger.Debug($"Процесс {processId.Value} не найден в системе");
                return false;
            }
            catch (Exception ex)
            {
                _logger.Warning($"Ошибка при проверке процесса {processId.Value}: {ex.Message}");
                return false; // В случае ошибки считаем процесс неактивным для безопасности
            }
        }

        /// <summary>
        /// Получение статистики для мониторинга
        /// </summary>
        public CleanupStatistics GetStatistics()
        {
            try
            {
                using var connection = new OdbcConnection(_connectionString);
                connection.Open();

                var stats = new CleanupStatistics
                {
                    TotalPrinters = connection.QuerySingle<int>(
                        PrinterSqlStore.GetPrinterStatistics,
                        commandTimeout: _commandTimeout),

                    AvailablePrinters = connection.QuerySingle<int>(
                        PrinterSqlStore.GetAvailablePrintersCount,
                        commandTimeout: _commandTimeout),

                    ReservedPrinters = connection.QuerySingle<int>(
                        PrinterSqlStore.GetReservedPrintersCount,
                        commandTimeout: _commandTimeout),

                    AverageReservationTimeMinutes = connection.QuerySingle<double>(
                        PrinterSqlStore.GetAverageReservationTime,
                        commandTimeout: _commandTimeout)
                };

                return stats;
            }
            catch (Exception ex)
            {
                _logger.Error($"Ошибка при получении статистики: {ex.Message}", ex);
                return new CleanupStatistics();
            }
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _cleanupTimer?.Dispose();
                _disposed = true;
                _logger.Information("BackgroundCleanupService disposed");
            }
        }
    }

    /// <summary>
    /// Статистика работы сервиса очистки
    /// </summary>
    public sealed class CleanupStatistics
    {
        public int TotalPrinters { get; set; }
        public int AvailablePrinters { get; set; }
        public int ReservedPrinters { get; set; }
        public double AverageReservationTimeMinutes { get; set; }

        public override string ToString()
        {
            return $"Принтеров: {TotalPrinters} (свободно: {AvailablePrinters}, " +
                   $"занято: {ReservedPrinters}), среднее время резервации: {AverageReservationTimeMinutes:F1} мин";
        }
    }
}