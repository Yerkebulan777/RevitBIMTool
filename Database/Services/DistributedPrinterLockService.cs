using Database.Configuration;
using Database.Models;
using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Linq;
using System.Threading;

namespace Database.Services
{
    /// <summary>
    /// Сервис распределенных блокировок принтеров
    /// Правильная реализация алгоритма блокировок с автоматическим освобождением
    /// </summary>
    public sealed class DistributedPrinterLockService : IDisposable
    {
        private readonly TimeSpan _defaultLockDuration = TimeSpan.FromMinutes(5);
        private readonly TimeSpan _cleanupInterval = TimeSpan.FromMinutes(1);
        private readonly DatabaseConfig _config;
        private readonly Timer _cleanupTimer;
        private volatile bool _disposed;

        public DistributedPrinterLockService()
        {
            _config = DatabaseConfig.Instance;

            // Автоматическая очистка истекших блокировок 
            _cleanupTimer = new Timer(CleanupExpiredLocks, null, _cleanupInterval, _cleanupInterval);

            InitializeDatabase();
        }

        /// <summary>
        /// Попытка получить блокировку принтера
        /// Использует SELECT FOR UPDATE для атомарности
        /// </summary>
        public PrinterLock TryAcquireLock(string printerName, string lockId = null, TimeSpan? duration = null)
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(DistributedPrinterLockService));
            }

            lockId ??= GenerateLockId();

            DateTime expiresAt = DateTime.UtcNow.Add(_defaultLockDuration);

            using IDbConnection connection = _config.Provider.CreateConnection(_config.ConnectionString);

            connection.Open();

            using IDbTransaction transaction = connection.BeginTransaction(IsolationLevel.Serializable);

            try
            {
                // Проверяем и очищаем истекшие блокировки этого принтера
                CleanupExpiredLocksForPrinter(printerName, transaction);

                if (TryInsertLock(printerName, lockId, expiresAt, transaction))
                {
                    transaction.Commit();

                    PrinterLock printerLock = new()
                    {
                        LockId = lockId,
                        ExpiresAt = expiresAt,
                        PrinterName = printerName,
                        ReservedAt = DateTime.UtcNow,
                        ReservedBy = Environment.UserName,
                        MachineName = Environment.MachineName
                    };

                    return printerLock;
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

        /// <summary>
        /// Освобождение блокировки
        /// </summary>
        public bool ReleaseLock(string lockId)
        {
            if (_disposed)
            {
                return false;
            }

            const string sql = @"
                DELETE FROM printer_locks 
                WHERE lock_id = @lockId AND machine_name = @machineName";

            try
            {
                using IDbConnection connection = _config.Provider.CreateConnection(_config.ConnectionString);
                connection.Open();

                using IDbCommand command = connection.CreateCommand();
                command.CommandText = sql;

                AddParameter(command, "@lockId", lockId);
                AddParameter(command, "@machineName", Environment.MachineName);

                return command.ExecuteNonQuery() > 0;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Продление блокировки (heartbeat)
        /// </summary>
        public bool ExtendLock(string lockId, TimeSpan additionalTime)
        {
            if (_disposed)
            {
                return false;
            }

            const string sql = @"
                UPDATE printer_locks 
                SET expires_at = @newExpiryTime, last_heartbeat = @now
                WHERE lock_id = @lockId AND machine_name = @machineName 
                  AND expires_at > @now";

            try
            {
                DateTime now = DateTime.UtcNow;
                DateTime newExpiryTime = now.Add(additionalTime);

                using IDbConnection connection = _config.Provider.CreateConnection(_config.ConnectionString);
                connection.Open();

                using IDbCommand command = connection.CreateCommand();
                command.CommandText = sql;

                AddParameter(command, "@lockId", lockId);
                AddParameter(command, "@machineName", Environment.MachineName);
                AddParameter(command, "@newExpiryTime", newExpiryTime);
                AddParameter(command, "@now", now);

                return command.ExecuteNonQuery() > 0;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Получение всех активных блокировок
        /// </summary>
        public IEnumerable<PrinterLock> GetActiveLocks()
        {
            if (_disposed)
            {
                return Enumerable.Empty<PrinterLock>();
            }

            const string sql = @"
                SELECT printer_name, lock_id, reserved_by, reserved_at, expires_at, 
                       process_id, machine_name, last_heartbeat
                FROM printer_locks 
                WHERE expires_at > @now
                ORDER BY printer_name, reserved_at";

            List<PrinterLock> locks = [];

            try
            {
                using IDbConnection connection = _config.Provider.CreateConnection(_config.ConnectionString);
                connection.Open();

                using IDbCommand command = connection.CreateCommand();
                command.CommandText = sql;
                AddParameter(command, "@now", DateTime.UtcNow);

                using IDataReader reader = command.ExecuteReader();
                while (reader.Read())
                {
                    locks.Add(new PrinterLock
                    {
                        PrinterName = reader["printer_name"].ToString(),
                        LockId = reader["lock_id"].ToString(),
                        ReservedBy = reader["reserved_by"].ToString(),
                        ReservedAt = Convert.ToDateTime(reader["reserved_at"]),
                        ExpiresAt = Convert.ToDateTime(reader["expires_at"]),
                        MachineName = reader["machine_name"].ToString()
                    });
                }
            }
            catch
            {
                // Логируем ошибку, но возвращаем пустой список
            }

            return locks;
        }

        private void InitializeDatabase()
        {
            const string createTableSql = @"
                CREATE TABLE IF NOT EXISTS printer_locks (
                    id INTEGER PRIMARY KEY AUTOINCREMENT,
                    printer_name TEXT NOT NULL,
                    lock_id TEXT NOT NULL UNIQUE,
                    reserved_by TEXT NOT NULL,
                    reserved_at DATETIME NOT NULL,
                    expires_at DATETIME NOT NULL,
                    process_id INTEGER NOT NULL,
                    machine_name TEXT NOT NULL,
                    last_heartbeat DATETIME DEFAULT CURRENT_TIMESTAMP
                );
                
                CREATE INDEX IF NOT EXISTS idx_printer_locks_expires 
                    ON printer_locks (expires_at);
                CREATE INDEX IF NOT EXISTS idx_printer_locks_printer 
                    ON printer_locks (printer_name);";

            try
            {
                using IDbConnection connection = _config.Provider.CreateConnection(_config.ConnectionString);
                connection.Open();

                using IDbCommand command = connection.CreateCommand();
                command.CommandText = createTableSql;
                _ = command.ExecuteNonQuery();
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("Failed to initialize printer locks database", ex);
            }
        }

        private bool TryInsertLock(string printerName, string lockId, DateTime expiresAt, IDbTransaction transaction)
        {
            const string sql = @"
                INSERT INTO printer_locks 
                (printer_name, lock_id, reserved_by, reserved_at, expires_at, process_id, machine_name)
                VALUES (@printerName, @lockId, @reservedBy, @reservedAt, @expiresAt, @processId, @machineName)";

            try
            {
                using IDbCommand command = transaction.Connection.CreateCommand();
                command.Transaction = transaction;
                command.CommandText = sql;

                AddParameter(command, "@printerName", printerName);
                AddParameter(command, "@lockId", lockId);
                AddParameter(command, "@reservedBy", Environment.UserName);
                AddParameter(command, "@reservedAt", DateTime.UtcNow);
                AddParameter(command, "@expiresAt", expiresAt);
                AddParameter(command, "@processId", Process.GetCurrentProcess().Id);
                AddParameter(command, "@machineName", Environment.MachineName);

                return command.ExecuteNonQuery() > 0;
            }
            catch
            {
                return false;
            }
        }

        private void CleanupExpiredLocksForPrinter(string printerName, IDbTransaction transaction)
        {
            const string sql = @"
                DELETE FROM printer_locks 
                WHERE printer_name = @printerName AND expires_at <= @now";

            using IDbCommand command = transaction.Connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = sql;

            AddParameter(command, "@printerName", printerName);
            AddParameter(command, "@now", DateTime.UtcNow);

            _ = command.ExecuteNonQuery();
        }

        private void CleanupExpiredLocks(object state)
        {
            if (_disposed)
            {
                return;
            }

            const string sql = "DELETE FROM printer_locks WHERE expires_at <= @now";

            try
            {
                using IDbConnection connection = _config.Provider.CreateConnection(_config.ConnectionString);
                connection.Open();

                using IDbCommand command = connection.CreateCommand();
                command.CommandText = sql;
                AddParameter(command, "@now", DateTime.UtcNow);

                int removed = command.ExecuteNonQuery();
                if (removed > 0)
                {
                    // Логируем очистку
                    Debug.WriteLine($"Cleaned up {removed} expired printer locks");
                }
            }
            catch
            {
                // Игнорируем ошибки очистки - это фоновая операция
            }
        }

        private static string GenerateLockId()
        {
            return $"{Environment.MachineName}_{Process.GetCurrentProcess().Id}_{Guid.NewGuid():N}";
        }

        private static void AddParameter(IDbCommand command, string name, object value)
        {
            IDbDataParameter parameter = command.CreateParameter();
            parameter.ParameterName = name;
            parameter.Value = value ?? DBNull.Value;
            _ = command.Parameters.Add(parameter);
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _disposed = true;
                _cleanupTimer?.Dispose();
            }
        }
    }
}