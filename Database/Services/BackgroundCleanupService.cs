using Dapper;
using Database.Logging;
using System;
using System.Data.Odbc;
using System.Diagnostics;
using System.Linq;
using System.Threading;

namespace Database.Services
{
    public sealed class BackgroundCleanupService : IDisposable
    {
        private readonly string _connectionString;
        private readonly ILogger _logger;
        private readonly Timer _cleanupTimer;
        private readonly TimeSpan _stuckThreshold;
        private bool _disposed;

        public BackgroundCleanupService(string connectionString, TimeSpan stuckThreshold)
        {
            _connectionString = connectionString;
            _stuckThreshold = stuckThreshold;
            _logger = LoggerFactory.CreateLogger<BackgroundCleanupService>();

            // Запуск очистки каждые 5 минут
            _cleanupTimer = new Timer(
                CleanupStuckReservations,
                null,
                TimeSpan.FromMinutes(1),
                TimeSpan.FromMinutes(5));
        }

        private void CleanupStuckReservations(object state)
        {
            try
            {
                _logger.Debug("Starting cleanup of stuck reservations");

                using var connection = new OdbcConnection(_connectionString);
                connection.Open();

                // Находим зависшие резервации
                const string findStuckSql = @"
                    SELECT printer_name, process_id, reserved_at,
                           EXTRACT(EPOCH FROM (CURRENT_TIMESTAMP - reserved_at))/60 as minutes_stuck
                    FROM printer_states
                    WHERE is_available = false
                      AND reserved_at < @cutoffTime
                      AND reserved_at IS NOT NULL;";

                var cutoffTime = DateTime.UtcNow.Subtract(_stuckThreshold);
                var stuckPrinters = connection.Query(findStuckSql, new { cutoffTime }).ToList();

                foreach (var stuck in stuckPrinters)
                {
                    _logger.Warning($"Found stuck printer: {stuck.printer_name}, " +
                                  $"stuck for {stuck.minutes_stuck:F1} minutes");

                    // Проверяем, жив ли процесс
                    if (IsProcessAlive(stuck.process_id))
                    {
                        _logger.Information($"Process {stuck.process_id} is still alive, skipping");
                        continue;
                    }

                    // Освобождаем принтер
                    ReleaseStuckPrinter(connection, stuck.printer_name);
                }

                if (stuckPrinters.Any())
                {
                    _logger.Information($"Cleaned up {stuckPrinters.Count} stuck reservations");
                }
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Cleanup failed");
            }
        }

        private bool IsProcessAlive(int? processId)
        {
            if (!processId.HasValue) return false;

            try
            {
                var process = Process.GetProcessById(processId.Value);
                return !process.HasExited;
            }
            catch
            {
                return false;
            }
        }

        private void ReleaseStuckPrinter(OdbcConnection connection, string printerName)
        {
            const string releaseSql = @"
                UPDATE printer_states SET
                    is_available = true,
                    reserved_file_name = NULL,
                    reserved_at = NULL,
                    process_id = NULL,
                    version_token = gen_random_uuid(),
                    state = 0,
                    last_update = CURRENT_TIMESTAMP
                WHERE printer_name = @printerName;";

            connection.Execute(releaseSql, new { printerName });

            // Логирование для аудита
            const string logSql = @"
                INSERT INTO printer_cleanup_log 
                (printer_name, cleaned_at, reason)
                VALUES (@printerName, CURRENT_TIMESTAMP, 'Stuck reservation timeout');";

            connection.Execute(logSql, new { printerName });
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _cleanupTimer?.Dispose();
                _disposed = true;
            }
        }
    }
}