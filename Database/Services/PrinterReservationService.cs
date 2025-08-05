using Dapper;
using Database.Models;
using Serilog;
using System.Data.Odbc;
using System.Diagnostics;

namespace Database.Services
{
    public sealed class PrinterReservationService : IDisposable
    {
        private readonly string _connectionString;
        private readonly ILogger _logger;
        private readonly Guid _sessionId;
        private bool _disposed;

        public PrinterReservationService(string connectionString)
        {
            _connectionString = connectionString;
            _logger = LoggerFactory.CreateLogger<PrinterReservationService>();
            _sessionId = Guid.NewGuid();
        }

        public PrinterReservation ReservePrinter(string printerName, string revitFileName)
        {
            _logger.Debug($"Reserving printer {printerName} for session {_sessionId}");

            using OdbcConnection connection = new(_connectionString);

            connection.Open();

            using OdbcTransaction transaction = connection.BeginTransaction();
            try
            {
                // Короткая транзакция только для резервирования
                PrinterReservation reservation = new()
                {
                    PrinterName = printerName,
                    RevitFileName = revitFileName,
                    ReservedAt = DateTime.UtcNow,
                    ProcessId = Process.GetCurrentProcess().Id,
                    SessionId = _sessionId,
                    State = ReservationState.Reserved
                };

                const string sql = @"
                    UPDATE printer_states SET
                        is_available = false,
                        reserved_file_name = @RevitFileName,
                        reserved_at = @ReservedAt,
                        process_id = @ProcessId,
                        version_token = @SessionId,
                        state = 1
                    WHERE printer_name = @PrinterName
                      AND is_available = true
                    RETURNING printer_name;";

                string updated = connection.QuerySingleOrDefault<string>(sql, reservation, transaction);

                if (updated != null)
                {
                    transaction.Commit();
                    _logger.Information($"Printer {printerName} reserved");
                    return reservation;
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

        public void UpdateProgress(string printerName, ReservationState state)
        {
            // Короткая транзакция для обновления статуса
            using OdbcConnection connection = new(_connectionString);
            connection.Open();

            const string sql = @"
                UPDATE printer_states 
                SET state = @state, 
                    last_update = CURRENT_TIMESTAMP
                WHERE printer_name = @printerName 
                  AND version_token = @sessionId;";

            _ = connection.Execute(sql, new { printerName, state = (int)state, _sessionId });

            _logger.Debug($"Updated {printerName} to state {state}");
        }

        public void ReleasePrinter(string printerName, bool success)
        {
            using OdbcConnection connection = new(_connectionString);

            connection.Open();

            ReservationState state = success ? ReservationState.Completed : ReservationState.Failed;

            const string sql = @"
                UPDATE printer_states SET
                    is_available = true,
                    reserved_file_name = NULL,
                    reserved_at = NULL,
                    process_id = NULL,
                    version_token = gen_random_uuid(),
                    state = 0,
                    last_update = CURRENT_TIMESTAMP
                WHERE printer_name = @printerName
                  AND version_token = @sessionId;";

            _ = connection.Execute(sql, new { printerName, _sessionId });

            _logger.Information($"Released {printerName} with status: {state}");
        }

        public void CompensateFailedReservation(PrinterReservation reservation)
        {
            if (reservation == null)
            {
                return;
            }

            _logger.Warning($"Compensating failed reservation for {reservation.PrinterName}");

            try
            {
                ReleasePrinter(reservation.PrinterName, false);
                // Логирование компенсации для аудита
                LogCompensation(reservation);
            }
            catch (Exception ex)
            {
                _logger.Error($"Compensation failed {ex.Message}", ex);
            }
        }

        private void LogCompensation(PrinterReservation reservation)
        {
            using OdbcConnection connection = new(_connectionString);
            connection.Open();

            const string sql = @"
                INSERT INTO printer_compensation_log 
                (printer_name, revit_file, session_id, compensated_at, reason)
                VALUES (@PrinterName, @RevitFileName, @SessionId, CURRENT_TIMESTAMP, 'Auto-compensation');";

            _ = connection.Execute(sql, reservation);
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _disposed = true;
            }
        }


    }
}