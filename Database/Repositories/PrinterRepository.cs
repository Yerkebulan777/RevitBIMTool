using Database.Configuration;
using Database.Models;
using Database.Providers;
using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;

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
            if (_config.Provider is InMemoryProvider inMemoryProvider)
            {
                return inMemoryProvider.GetAvailablePrinters();
            }

            return GetAvailablePrintersFromDatabase(transaction);
        }

        public bool TryReservePrinter(string printerName, string reservedBy, IDbTransaction transaction = null)
        {
            if (_config.Provider is InMemoryProvider inMemoryProvider)
            {
                return inMemoryProvider.TryReservePrinter(printerName, reservedBy);
            }

            return TryReservePrinterInDatabase(printerName, reservedBy, transaction);
        }

        public bool ReleasePrinter(string printerName, IDbTransaction transaction = null)
        {
            if (_config.Provider is InMemoryProvider inMemoryProvider)
            {
                return inMemoryProvider.ReleasePrinter(printerName);
            }

            return ReleasePrinterInDatabase(printerName, transaction);
        }

        public bool UpsertPrinter(PrinterState printerState, IDbTransaction transaction = null)
        {
            // Для in-memory провайдера эта операция не имеет смысла
            // Принтеры создаются при инициализации
            if (_config.Provider is InMemoryProvider)
            {
                return true;
            }

            return UpsertPrinterInDatabase(printerState, transaction);
        }

        public int CleanupExpiredReservations(TimeSpan expiredAfter, IDbTransaction transaction = null)
        {
            if (_config.Provider is InMemoryProvider inMemoryProvider)
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

            using (var connection = transaction?.Connection ?? _config.Provider.CreateConnection(_config.ConnectionString))
            {
                bool shouldCloseConnection = transaction == null;

                try
                {
                    if (connection.State != ConnectionState.Open)
                        connection.Open();

                    using (var command = connection.CreateCommand())
                    {
                        command.Transaction = transaction;
                        command.CommandText = sql;
                        command.CommandTimeout = _config.CommandTimeout;

                        var parameter = CreateParameter(command, "@printerName", printerName);
                        command.Parameters.Add(parameter);

                        using (var reader = command.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                return MapFromReader(reader);
                            }
                        }
                    }
                }
                finally
                {
                    if (shouldCloseConnection && connection.State == ConnectionState.Open)
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

            var results = new List<PrinterState>();

            using (var connection = transaction?.Connection ?? _config.Provider.CreateConnection(_config.ConnectionString))
            {
                bool shouldCloseConnection = transaction == null;

                try
                {
                    if (connection.State != ConnectionState.Open)
                        connection.Open();

                    using (var command = connection.CreateCommand())
                    {
                        command.Transaction = transaction;
                        command.CommandText = sql;
                        command.CommandTimeout = _config.CommandTimeout;

                        using (var reader = command.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                results.Add(MapFromReader(reader));
                            }
                        }
                    }
                }
                finally
                {
                    if (shouldCloseConnection && connection.State == ConnectionState.Open)
                        connection.Close();
                }
            }

            return results;
        }

        private bool TryReservePrinterInDatabase(string printerName, string reservedBy, IDbTransaction transaction)
        {
            using (var connection = transaction?.Connection ?? _config.Provider.CreateConnection(_config.ConnectionString))
            {
                bool shouldCloseConnection = transaction == null;
                IDbTransaction localTransaction = transaction;

                try
                {
                    if (connection.State != ConnectionState.Open)
                        connection.Open();

                    if (localTransaction == null)
                    {
                        localTransaction = connection.BeginTransaction();
                    }

                    // Используем SQL, специфичный для данного провайдера
                    var selectSql = _config.Provider.GetReservePrinterScript();
                    bool isAvailable = false;

                    using (var selectCommand = connection.CreateCommand())
                    {
                        selectCommand.Transaction = localTransaction;
                        selectCommand.CommandText = selectSql;
                        selectCommand.CommandTimeout = _config.CommandTimeout;

                        var parameter = CreateParameter(selectCommand, "@printerName", printerName);
                        selectCommand.Parameters.Add(parameter);

                        using (var reader = selectCommand.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                var availableValue = reader["is_available"];
                                isAvailable = Convert.ToBoolean(availableValue);
                            }
                        }
                    }

                    if (!isAvailable)
                    {
                        if (transaction == null)
                            localTransaction.Rollback();
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

                    using (var updateCommand = connection.CreateCommand())
                    {
                        updateCommand.Transaction = localTransaction;
                        updateCommand.CommandText = updateSql;
                        updateCommand.CommandTimeout = _config.CommandTimeout;

                        AddReservationParameters(updateCommand, printerName, reservedBy);

                        int rowsAffected = updateCommand.ExecuteNonQuery();

                        if (rowsAffected > 0)
                        {
                            if (transaction == null)
                                localTransaction.Commit();
                            return true;
                        }
                        else
                        {
                            if (transaction == null)
                                localTransaction.Rollback();
                            return false;
                        }
                    }
                }
                catch (Exception)
                {
                    if (transaction == null && localTransaction != null)
                    {
                        try { localTransaction.Rollback(); } catch { }
                    }
                    throw;
                }
                finally
                {
                    if (shouldCloseConnection && connection.State == ConnectionState.Open)
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

            using (var connection = transaction?.Connection ?? _config.Provider.CreateConnection(_config.ConnectionString))
            {
                bool shouldCloseConnection = transaction == null;

                try
                {
                    if (connection.State != ConnectionState.Open)
                        connection.Open();

                    using (var command = connection.CreateCommand())
                    {
                        command.Transaction = transaction;
                        command.CommandText = sql;
                        command.CommandTimeout = _config.CommandTimeout;

                        var printerParam = CreateParameter(command, "@printerName", printerName);
                        command.Parameters.Add(printerParam);

                        var timeParam = CreateParameter(command, "@lastUpdated", FormatDateTime(DateTime.UtcNow));
                        command.Parameters.Add(timeParam);

                        int rowsAffected = command.ExecuteNonQuery();
                        return rowsAffected > 0;
                    }
                }
                finally
                {
                    if (shouldCloseConnection && connection.State == ConnectionState.Open)
                        connection.Close();
                }
            }
        }

        private bool UpsertPrinterInDatabase(PrinterState printerState, IDbTransaction transaction)
        {
            // Упрощенная реализация: сначала UPDATE, потом INSERT если нужно
            // Можно расширить для конкретных СУБД
            return true; // Заглушка для демонстрации
        }

        private int CleanupExpiredReservationsInDatabase(TimeSpan expiredAfter, IDbTransaction transaction)
        {
            // Аналогично другим методам
            return 0; // Заглушка
        }

        private void InitializePrintersInDatabase(IEnumerable<string> printerNames, IDbTransaction transaction)
        {
            // Аналогично другим методам
        }

        #endregion

        #region Helper methods

        private IDbDataParameter CreateParameter(IDbCommand command, string name, object value)
        {
            var parameter = command.CreateParameter();
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

        private DateTime? ParseDateTime(object value)
        {
            if (value == null || value == DBNull.Value)
                return null;

            if (value is DateTime dateTime)
                return dateTime;

            if (value is string dateString && DateTime.TryParse(dateString, out DateTime parsed))
                return parsed;

            return null;
        }

        private string FormatDateTime(DateTime dateTime)
        {
            return dateTime.ToString("yyyy-MM-dd HH:mm:ss");
        }

        private void AddReservationParameters(IDbCommand command, string printerName, string reservedBy)
        {
            var currentProcess = Process.GetCurrentProcess();
            var now = DateTime.UtcNow;

            command.Parameters.Add(CreateParameter(command, "@printerName", printerName));
            command.Parameters.Add(CreateParameter(command, "@reservedBy", reservedBy));
            command.Parameters.Add(CreateParameter(command, "@reservedAt", FormatDateTime(now)));
            command.Parameters.Add(CreateParameter(command, "@lastUpdated", FormatDateTime(now)));
            command.Parameters.Add(CreateParameter(command, "@processId", currentProcess.Id));
            command.Parameters.Add(CreateParameter(command, "@machineName", Environment.MachineName));
        }

        #endregion
    }
}