using Database.Configuration;
using Database.Models;
using Database.Providers;
using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Globalization;

namespace Database.Repositories
{
    public class PrinterRepository : IPrinterRepository
    {
        private readonly DatabaseConfig _config;

        public PrinterRepository()
        {
            _config = DatabaseConfig.Instance;
        }

        public PrinterState GetByName(string printerName, IDbTransaction transaction = null)
        {
            // Проверяем, работаем ли мы с in-memory провайдером
            if (_config.Provider is InMemoryProvider inMemoryProvider)
            {
                return inMemoryProvider.GetPrinter(printerName);
            }

            // Для всех остальных провайдеров используем стандартный SQL
            return GetByNameFromDatabase(printerName, transaction);
        }

        public IEnumerable<PrinterState> GetAvailablePrinters(IDbTransaction transaction = null)
        {
            return _config.Provider is InMemoryProvider inMemoryProvider
                ? inMemoryProvider.GetAvailablePrinters()
                : GetAvailablePrintersFromDatabase(transaction);
        }

        public bool TryReservePrinter(string printerName, string reservedBy, IDbTransaction transaction = null)
        {
            return _config.Provider is InMemoryProvider inMemoryProvider
                ? inMemoryProvider.TryReservePrinter(printerName, reservedBy)
                : TryReservePrinterInDatabase(printerName, reservedBy, transaction);
        }

        public bool ReleasePrinter(string printerName, IDbTransaction transaction = null)
        {
            return _config.Provider is InMemoryProvider inMemoryProvider
                ? inMemoryProvider.ReleasePrinter(printerName)
                : ReleasePrinterInDatabase(printerName, transaction);
        }

        public int CleanupExpiredReservations(TimeSpan expiredAfter, IDbTransaction transaction = null)
        {
            if (_config.Provider is InMemoryProvider)
            {
                // In-memory провайдер не требует очистки
                return 0;
            }

            return CleanupExpiredReservationsInDatabase(expiredAfter, transaction);
        }

        public void InitializePrinters(IEnumerable<string> printerNames, IDbTransaction transaction = null)
        {
            if (_config.Provider is InMemoryProvider)
            {
                // In-memory провайдер инициализируется автоматически
                return;
            }

            InitializePrintersInDatabase(printerNames, transaction);
        }

        #region SQL-based implementations

        /// <summary>
        /// Универсальная реализация для всех SQL провайдеров
        /// Использует только стандартные методы интерфейса IDatabaseProvider
        /// </summary>
        private PrinterState GetByNameFromDatabase(string printerName, IDbTransaction transaction)
        {
            const string sql = @"
                SELECT id, printer_name, is_available, reserved_by, reserved_at, 
                       last_updated, process_id, machine_name, version
                FROM printer_states 
                WHERE printer_name = @printerName";

            using IDbConnection connection = transaction?.Connection ??
                _config.Provider.CreateConnection(_config.ConnectionString);
            bool shouldCloseConnection = transaction == null;

            try
            {
                if (connection.State != ConnectionState.Open)
                {
                    connection.Open();
                }

                using IDbCommand command = connection.CreateCommand();
                command.Transaction = transaction;
                command.CommandText = sql;
                command.CommandTimeout = _config.CommandTimeout;

                IDbDataParameter parameter = CreateParameter(command, "@printerName", printerName);
                _ = command.Parameters.Add(parameter);

                using IDataReader reader = command.ExecuteReader();
                if (reader.Read())
                {
                    return MapFromReader(reader);
                }
            }
            finally
            {
                if (shouldCloseConnection && connection.State == ConnectionState.Open)
                {
                    connection.Close();
                }
            }

            return null;
        }

        private IEnumerable<PrinterState> GetAvailablePrintersFromDatabase(IDbTransaction transaction)
        {
            const string sql = @"
                SELECT id, printer_name, is_available, reserved_by, reserved_at, 
                       last_updated, process_id, machine_name, version
                FROM printer_states 
                WHERE is_available = 1 
                ORDER BY printer_name";

            List<PrinterState> results = [];

            using IDbConnection connection = transaction?.Connection ??
                _config.Provider.CreateConnection(_config.ConnectionString);
            bool shouldCloseConnection = transaction == null;

            try
            {
                if (connection.State != ConnectionState.Open)
                {
                    connection.Open();
                }

                using IDbCommand command = connection.CreateCommand();
                command.Transaction = transaction;
                command.CommandText = sql;
                command.CommandTimeout = _config.CommandTimeout;

                using IDataReader reader = command.ExecuteReader();
                while (reader.Read())
                {
                    results.Add(MapFromReader(reader));
                }
            }
            finally
            {
                if (shouldCloseConnection && connection.State == ConnectionState.Open)
                {
                    connection.Close();
                }
            }

            return results;
        }

        public bool TryReservePrinterInDatabase(string printerName, string reservedBy, IDbTransaction transaction)
        {
            using IDbConnection connection = transaction?.Connection ??
                _config.Provider.CreateConnection(_config.ConnectionString);
            bool shouldCloseConnection = transaction == null;
            IDbTransaction localTransaction = transaction;

            try
            {
                if (connection.State != ConnectionState.Open)
                {
                    connection.Open();
                }

                // Создаем локальную транзакцию если не передана извне
                localTransaction ??= connection.BeginTransaction();

                // Этап 1: Используем SELECT FOR UPDATE из провайдера
                string selectSql = _config.Provider.GetReservePrinterScript();
                long currentVersion = 0;
                bool printerFound = false;

                using (IDbCommand selectCommand = connection.CreateCommand())
                {
                    selectCommand.Transaction = localTransaction;
                    selectCommand.CommandText = selectSql;
                    selectCommand.CommandTimeout = _config.CommandTimeout;

                    IDbDataParameter parameter = CreateParameter(selectCommand, "@printerName", printerName);
                    _ = selectCommand.Parameters.Add(parameter);

                    using IDataReader reader = selectCommand.ExecuteReader();
                    if (reader.Read())
                    {
                        printerFound = true;
                        currentVersion = Convert.ToInt64(reader["version"]);
                    }
                }

                if (!printerFound)
                {
                    if (transaction == null)
                    {
                        localTransaction.Rollback();
                    }
                    return false;
                }

                // Этап 2: Обновляем с проверкой версии (универсальный SQL)
                string updateSql = GetUniversalUpdateScript();

                using IDbCommand updateCommand = connection.CreateCommand();
                updateCommand.Transaction = localTransaction;
                updateCommand.CommandText = updateSql;
                updateCommand.CommandTimeout = _config.CommandTimeout;

                AddReservationParameters(updateCommand, printerName, reservedBy, currentVersion);

                int rowsAffected = updateCommand.ExecuteNonQuery();

                if (rowsAffected > 0)
                {
                    if (transaction == null)
                    {
                        localTransaction.Commit();
                    }
                    return true;
                }
                else
                {
                    if (transaction == null)
                    {
                        localTransaction.Rollback();
                    }
                    return false;
                }
            }
            catch (Exception)
            {
                if (transaction == null && localTransaction != null)
                {
                    localTransaction.Rollback();
                }
                throw;
            }
            finally
            {
                if (shouldCloseConnection && connection.State == ConnectionState.Open)
                {
                    connection.Close();
                }
            }
        }

        /// <summary>
        /// Универсальный SQL для обновления, работающий с любыми провайдерами
        /// </summary>
        private static string GetUniversalUpdateScript()
        {
            return @"
                UPDATE printer_states 
                SET is_available = 0,
                    reserved_by = @reservedBy,
                    reserved_at = @reservedAt,
                    last_updated = @lastUpdated,
                    process_id = @processId,
                    machine_name = @machineName,
                    version = version + 1
                WHERE printer_name = @printerName 
                  AND is_available = 1
                  AND version = @expectedVersion";
        }

        private static void AddReservationParameters(IDbCommand command, string printerName,
            string reservedBy, long expectedVersion)
        {
            Process currentProcess = Process.GetCurrentProcess();
            DateTime now = DateTime.UtcNow;

            _ = command.Parameters.Add(CreateParameter(command, "@printerName", printerName));
            _ = command.Parameters.Add(CreateParameter(command, "@reservedBy", reservedBy));
            _ = command.Parameters.Add(CreateParameter(command, "@reservedAt", FormatDateTime(now)));
            _ = command.Parameters.Add(CreateParameter(command, "@lastUpdated", FormatDateTime(now)));
            _ = command.Parameters.Add(CreateParameter(command, "@processId", currentProcess.Id));
            _ = command.Parameters.Add(CreateParameter(command, "@machineName", Environment.MachineName));
            _ = command.Parameters.Add(CreateParameter(command, "@expectedVersion", expectedVersion));
        }

        private bool ReleasePrinterInDatabase(string printerName, IDbTransaction transaction)
        {
            const string sql = @"
                UPDATE printer_states 
                SET is_available = 1,
                    reserved_by = NULL,
                    reserved_at = NULL,
                    last_updated = @lastUpdated,
                    process_id = NULL,
                    machine_name = NULL,
                    version = version + 1
                WHERE printer_name = @printerName";

            using IDbConnection connection = transaction?.Connection ??
                _config.Provider.CreateConnection(_config.ConnectionString);
            bool shouldCloseConnection = transaction == null;

            try
            {
                if (connection.State != ConnectionState.Open)
                {
                    connection.Open();
                }

                using IDbCommand command = connection.CreateCommand();
                command.Transaction = transaction;
                command.CommandText = sql;
                command.CommandTimeout = _config.CommandTimeout;

                IDbDataParameter printerParam = CreateParameter(command, "@printerName", printerName);
                _ = command.Parameters.Add(printerParam);

                IDbDataParameter timeParam = CreateParameter(command, "@lastUpdated", FormatDateTime(DateTime.UtcNow));
                _ = command.Parameters.Add(timeParam);

                int rowsAffected = command.ExecuteNonQuery();
                return rowsAffected > 0;
            }
            finally
            {
                if (shouldCloseConnection && connection.State == ConnectionState.Open)
                {
                    connection.Close();
                }
            }
        }

        private int CleanupExpiredReservationsInDatabase(TimeSpan expiredAfter, IDbTransaction transaction)
        {
            DateTime cutoffTime = DateTime.UtcNow.Subtract(expiredAfter);

            const string sql = @"
                UPDATE printer_states 
                SET is_available = 1,
                    reserved_by = NULL,
                    reserved_at = NULL,
                    last_updated = @now,
                    process_id = NULL,
                    machine_name = NULL,
                    version = version + 1
                WHERE is_available = 0 
                  AND reserved_at < @cutoffTime";

            using IDbConnection connection = transaction?.Connection ??
                _config.Provider.CreateConnection(_config.ConnectionString);
            bool shouldCloseConnection = transaction == null;

            try
            {
                if (connection.State != ConnectionState.Open)
                {
                    connection.Open();
                }

                using IDbCommand command = connection.CreateCommand();
                command.Transaction = transaction;
                command.CommandText = sql;
                command.CommandTimeout = _config.CommandTimeout;

                _ = command.Parameters.Add(CreateParameter(command, "@cutoffTime", FormatDateTime(cutoffTime)));
                _ = command.Parameters.Add(CreateParameter(command, "@now", FormatDateTime(DateTime.UtcNow)));

                return command.ExecuteNonQuery();
            }
            finally
            {
                if (shouldCloseConnection && connection.State == ConnectionState.Open)
                {
                    connection.Close();
                }
            }
        }

        private void InitializePrintersInDatabase(IEnumerable<string> printerNames, IDbTransaction transaction)
        {
            const string checkSql = "SELECT COUNT(*) FROM printer_states WHERE printer_name = @printerName";
            const string insertSql = @"
                INSERT INTO printer_states (printer_name, is_available, last_updated, version)
                VALUES (@printerName, 1, @lastUpdated, 1)";

            using IDbConnection connection = transaction?.Connection ??
                _config.Provider.CreateConnection(_config.ConnectionString);
            bool shouldCloseConnection = transaction == null;

            try
            {
                if (connection.State != ConnectionState.Open)
                {
                    connection.Open();
                }

                foreach (string printerName in printerNames)
                {
                    // Проверяем, существует ли принтер
                    using IDbCommand checkCommand = connection.CreateCommand();
                    checkCommand.Transaction = transaction;
                    checkCommand.CommandText = checkSql;
                    _ = checkCommand.Parameters.Add(CreateParameter(checkCommand, "@printerName", printerName));

                    object result = checkCommand.ExecuteScalar();
                    int count = Convert.ToInt32(result);

                    if (count == 0)
                    {
                        // Добавляем новый принтер
                        using IDbCommand insertCommand = connection.CreateCommand();
                        insertCommand.Transaction = transaction;
                        insertCommand.CommandText = insertSql;
                        _ = insertCommand.Parameters.Add(CreateParameter(insertCommand, "@printerName", printerName));
                        _ = insertCommand.Parameters.Add(CreateParameter(insertCommand, "@lastUpdated", FormatDateTime(DateTime.UtcNow)));

                        _ = insertCommand.ExecuteNonQuery();
                    }
                }
            }
            finally
            {
                if (shouldCloseConnection && connection.State == ConnectionState.Open)
                {
                    connection.Close();
                }
            }
        }

        #endregion

        #region Helper methods

        private static IDbDataParameter CreateParameter(IDbCommand command, string name, object value)
        {
            IDbDataParameter parameter = command.CreateParameter();
            parameter.ParameterName = name;
            parameter.Value = value ?? DBNull.Value;
            return parameter;
        }

        private PrinterState MapFromReader(IDataReader reader)
        {
            return new PrinterState
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
            };
        }

        private static DateTime? ParseDateTime(object value)
        {
            if (value is DateTime dateTime)
            {
                return dateTime;
            }
            if (value is string dateString && !string.IsNullOrEmpty(dateString))
            {
                Debug.WriteLine($"Parsing date string: {dateString}");

                if (DateTime.TryParse(dateString, CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime parsed))
                {
                    return parsed;
                }
            }
            return null;
        }

        private static string FormatDateTime(DateTime dateTime)
        {
            return dateTime.ToString("yyyy-MM-dd HH:mm:ss");
        }

        #endregion
    }
}