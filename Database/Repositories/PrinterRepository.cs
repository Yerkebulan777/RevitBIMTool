using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using Database.Configuration;
using Database.Models;

namespace Database.Repositories
{
    /// <summary>
    /// Универсальный репозиторий, работающий с любой базой данных
    /// Использует абстракцию провайдера для скрытия специфики конкретной СУБД
    /// </summary>
    public class PrinterRepository : IPrinterRepository
    {
        private readonly DatabaseConfig _config;

        public PrinterRepository()
        {
            _config = DatabaseConfig.Instance;
        }

        /// <summary>
        /// Создание подключения через провайдер
        /// Провайдер знает, как правильно создать подключение для своей СУБД
        /// </summary>
        private IDbConnection CreateConnection()
        {
            return _config.Provider.CreateConnection(_config.ConnectionString);
        }

        public PrinterState GetByName(string printerName, IDbTransaction transaction = null)
        {
            const string sql = @"
                SELECT id, printer_name, is_available, reserved_by, reserved_at, 
                       last_updated, process_id, machine_name, version
                FROM printer_states 
                WHERE printer_name = @printerName";

            using (var connection = transaction?.Connection ?? CreateConnection())
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

        /// <summary>
        /// Ключевой метод для резервирования принтера
        /// Адаптируется к возможностям конкретной СУБД
        /// </summary>
        public bool TryReservePrinter(string printerName, string reservedBy, IDbTransaction transaction = null)
        {
            using (var connection = transaction?.Connection ?? CreateConnection())
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

                    // Используем SQL, подходящий для данной СУБД
                    var selectSql = _config.Provider.GetReservePrinterScript();
                    bool isAvailable = false;

                    // Этап 1: Проверяем доступность (с блокировкой, если поддерживается)
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
                                isAvailable = Convert.ToBoolean(reader["is_available"]);
                            }
                        }
                    }

                    if (!isAvailable)
                    {
                        if (transaction == null)
                            localTransaction.Rollback();
                        return false;
                    }

                    // Этап 2: Резервируем принтер
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

        // Остальные методы реализуются аналогично...
        // ReleasePrinter, GetAvailablePrinters, CleanupExpiredReservations, InitializePrinters

        /// <summary>
        /// Универсальное создание параметра команды
        /// Разные провайдеры могут требовать разные типы параметров
        /// </summary>
        private IDbDataParameter CreateParameter(IDbCommand command, string name, object value)
        {
            var parameter = command.CreateParameter();
            parameter.ParameterName = name;
            parameter.Value = value ?? DBNull.Value;
            return parameter;
        }

        /// <summary>
        /// Маппинг данных из DataReader с обработкой различий между СУБД
        /// </summary>
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

        /// <summary>
        /// Универсальный парсинг даты-времени
        /// Разные СУБД могут хранить даты в разных форматах
        /// </summary>
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

        private void AddReservationParameters(IDbCommand command, string printerName, string reservedBy)
        {
            var currentProcess = Process.GetCurrentProcess();
            var now = DateTime.UtcNow;

            command.Parameters.Add(CreateParameter(command, "@printerName", printerName));
            command.Parameters.Add(CreateParameter(command, "@reservedBy", reservedBy));
            command.Parameters.Add(CreateParameter(command, "@reservedAt", now.ToString("yyyy-MM-dd HH:mm:ss")));
            command.Parameters.Add(CreateParameter(command, "@lastUpdated", now.ToString("yyyy-MM-dd HH:mm:ss")));
            command.Parameters.Add(CreateParameter(command, "@processId", currentProcess.Id));
            command.Parameters.Add(CreateParameter(command, "@machineName", Environment.MachineName));
        }
    }
}