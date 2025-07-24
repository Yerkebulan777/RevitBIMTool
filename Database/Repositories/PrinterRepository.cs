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
    /// <summary>
    /// Универсальный репозиторий, который умеет работать с любыми провайдерами
    /// Ключевая особенность: он проверяет тип провайдера и выбирает оптимальную стратегию
    /// </summary>
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

            // Обычная SQL логика для других провайдеров
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

        public bool UpsertPrinter(PrinterState printerState, IDbTransaction transaction = null)
        {
            // Для in-memory провайдера эта операция не имеет смысла
            // Принтеры создаются при инициализации
            return _config.Provider is InMemoryProvider || UpsertPrinterInDatabase(printerState, transaction);
        }

        public int CleanupExpiredReservations(TimeSpan expiredAfter, IDbTransaction transaction = null)
        {
            if (_config.Provider is InMemoryProvider)
            {
                // Для демонстрации можно добавить логику очистки in-memory данных
                return 0;
            }

            return CleanupExpiredReservationsInDatabase(expiredAfter, transaction);
        }

        public void InitializePrinters(IEnumerable<string> printerNames, IDbTransaction transaction = null)
        {
            if (_config.Provider is InMemoryProvider)
            {
                // In-memory провайдер инициализируется сам
                return;
            }

            InitializePrintersInDatabase(printerNames, transaction);
        }

        #region SQL-based implementations

        /// <summary>
        /// Реализация для обычных SQL баз данных
        /// Эти методы используют стандартные ADO.NET интерфейсы
        /// </summary>
        private PrinterState GetByNameFromDatabase(string printerName, IDbTransaction transaction)
        {
            const string sql = @"
                SELECT id, printer_name, is_available, reserved_by, reserved_at, 
                       last_updated, process_id, machine_name, version
                FROM printer_states 
                WHERE printer_name = @printerName";

            using IDbConnection connection = transaction?.Connection ?? _config.Provider.CreateConnection(_config.ConnectionString);
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

            using (IDbConnection connection = transaction?.Connection ?? _config.Provider.CreateConnection(_config.ConnectionString))
            {
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
            }

            return results;
        }

        private bool TryReservePrinterInDatabase(string printerName, string reservedBy, IDbTransaction transaction)
        {
            using IDbConnection connection = transaction?.Connection ?? _config.Provider.CreateConnection(_config.ConnectionString);
            bool shouldCloseConnection = transaction == null;
            IDbTransaction localTransaction = transaction;

            try
            {
                if (connection.State != ConnectionState.Open)
                {
                    connection.Open();
                }

                localTransaction ??= connection.BeginTransaction();

                // Используем SQL, специфичный для данного провайдера
                string selectSql = _config.Provider.GetReservePrinterScript();
                bool isAvailable = false;

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
                        object availableValue = reader["is_available"];
                        isAvailable = Convert.ToBoolean(availableValue);
                    }
                }

                if (!isAvailable)
                {
                    if (transaction == null)
                    {
                        localTransaction.Rollback();
                    }

                    return false;
                }

                const string updateSql = @"
                        UPDATE printer_states 
                        SET is_available = 0,
                            reserved_by = @reservedBy,
                            reserved_at = @reservedAt,
                            last_updated = @lastUpdated,
                            process_id = @processId,
                            machine_name = @machineName,
                            version = version + 1
                        WHERE printer_name = @printerName 
                          AND is_available = 1";

                using IDbCommand updateCommand = connection.CreateCommand();
                updateCommand.Transaction = localTransaction;
                updateCommand.CommandText = updateSql;
                updateCommand.CommandTimeout = _config.CommandTimeout;

                AddReservationParameters(updateCommand, printerName, reservedBy);

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

            using IDbConnection connection = transaction?.Connection ?? _config.Provider.CreateConnection(_config.ConnectionString);
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

        private static bool UpsertPrinterInDatabase(PrinterState printerState, IDbTransaction transaction)
        {
            Debug.Assert(printerState != null, "Printer state must not be null");
            Debug.Assert(transaction != null, "Transaction must not be null");
            // Упрощенная реализация: сначала UPDATE, потом INSERT если нужно
            // Можно расширить для конкретных СУБД
            return true; // Заглушка для демонстрации
        }

        private static int CleanupExpiredReservationsInDatabase(TimeSpan expiredAfter, IDbTransaction transaction)
        {
            Debug.Assert(expiredAfter.TotalSeconds > 0, "Expired time must be greater than zero");
            Debug.Assert(transaction != null, "Transaction must not be null");
            // Аналогично другим методам
            return 0; // Заглушка
        }

        private static void InitializePrintersInDatabase(IEnumerable<string> printerNames, IDbTransaction transaction)
        {
            Debug.Assert(printerNames != null, "Printer names must not be null");
            Debug.Assert(transaction != null, "Transaction must not be null");
            // Аналогично другим методам
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

            return value is string dateString && DateTime.TryParse(dateString, CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime parsed)
                ? parsed
                : (DateTime?)null;
        }

        private static string FormatDateTime(DateTime dateTime)
        {
            return dateTime.ToString("yyyy-MM-dd HH:mm:ss");
        }

        private static void AddReservationParameters(IDbCommand command, string printerName, string reservedBy)
        {
            Process currentProcess = Process.GetCurrentProcess();
            DateTime now = DateTime.UtcNow;

            _ = command.Parameters.Add(CreateParameter(command, "@printerName", printerName));
            _ = command.Parameters.Add(CreateParameter(command, "@reservedBy", reservedBy));
            _ = command.Parameters.Add(CreateParameter(command, "@reservedAt", FormatDateTime(now)));
            _ = command.Parameters.Add(CreateParameter(command, "@lastUpdated", FormatDateTime(now)));
            _ = command.Parameters.Add(CreateParameter(command, "@processId", currentProcess.Id));
            _ = command.Parameters.Add(CreateParameter(command, "@machineName", Environment.MachineName));
        }

        #endregion
    }
}