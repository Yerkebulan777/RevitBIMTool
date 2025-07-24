// Database/Repositories/PrinterRepository.cs
using Database.Configuration;
using Database.Models;
using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;

namespace Database.Repositories
{
    /// <summary>
    /// Реализация репозитория для работы с принтерами через PostgreSQL
    /// Использует ADO.NET для максимальной производительности и контроля
    /// </summary>
    public class PrinterRepository : IPrinterRepository
    {
        private readonly DatabaseConfig _config;

        public PrinterRepository()
        {
            _config = DatabaseConfig.Instance;
        }

        /// <summary>
        /// Создает подключение к базе данных с использованием паттерна Factory
        /// Connection pooling управляется драйвером Npgsql автоматически
        /// </summary>
        private IDbConnection CreateConnection()
        {
            return new NpgsqlConnection(_config.ConnectionString);
        }

        /// <summary>
        /// Получение принтера по имени с оптимальным SQL запросом
        /// Индекс по printer_name обеспечивает быстрый поиск
        /// </summary>
        public PrinterState GetByName(string printerName, IDbTransaction transaction = null)
        {
            const string sql = @"
                SELECT id, printer_name, is_available, reserved_by, reserved_at, 
                       last_updated, process_id, machine_name, version
                FROM printer_states 
                WHERE printer_name = @printerName";

            using (IDbConnection connection = transaction?.Connection ?? CreateConnection())
            {
                bool shouldCloseConnection = transaction == null;

                try
                {
                    if (connection.State != ConnectionState.Open)
                    {
                        connection.Open();
                    }

                    using (IDbCommand command = connection.CreateCommand())
                    {
                        command.Transaction = transaction;
                        command.CommandText = sql;
                        command.CommandTimeout = _config.CommandTimeout;

                        IDbDataParameter parameter = command.CreateParameter();
                        parameter.ParameterName = "@printerName";
                        parameter.Value = printerName;
                        _ = command.Parameters.Add(parameter);

                        using (IDataReader reader = command.ExecuteReader())
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
                    {
                        connection.Close();
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// Получение всех доступных принтеров
        /// Фильтрация на уровне базы данных для оптимальной производительности
        /// </summary>
        public IEnumerable<PrinterState> GetAvailablePrinters(IDbTransaction transaction = null)
        {
            const string sql = @"
                SELECT id, printer_name, is_available, reserved_by, reserved_at, 
                       last_updated, process_id, machine_name, version
                FROM printer_states 
                WHERE is_available = true 
                ORDER BY printer_name";

            List<PrinterState> results = new List<PrinterState>();

            using (IDbConnection connection = transaction?.Connection ?? CreateConnection())
            {
                bool shouldCloseConnection = transaction == null;

                try
                {
                    if (connection.State != ConnectionState.Open)
                    {
                        connection.Open();
                    }

                    using (IDbCommand command = connection.CreateCommand())
                    {
                        command.Transaction = transaction;
                        command.CommandText = sql;
                        command.CommandTimeout = _config.CommandTimeout;

                        using (IDataReader reader = command.ExecuteReader())
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
                    {
                        connection.Close();
                    }
                }
            }

            return results;
        }

        /// <summary>
        /// Upsert операция с оптимистичным блокированием
        /// PostgreSQL ON CONFLICT обеспечивает атомарность операции
        /// </summary>
        public bool UpsertPrinter(PrinterState printerState, IDbTransaction transaction = null)
        {
            const string sql = @"
                INSERT INTO printer_states 
                    (printer_name, is_available, reserved_by, reserved_at, last_updated, process_id, machine_name, version)
                VALUES 
                    (@printerName, @isAvailable, @reservedBy, @reservedAt, @lastUpdated, @processId, @machineName, 1)
                ON CONFLICT (printer_name) 
                DO UPDATE SET 
                    is_available = @isAvailable,
                    reserved_by = @reservedBy,
                    reserved_at = @reservedAt,
                    last_updated = @lastUpdated,
                    process_id = @processId,
                    machine_name = @machineName,
                    version = printer_states.version + 1
                WHERE printer_states.version = @currentVersion";

            using (IDbConnection connection = transaction?.Connection ?? CreateConnection())
            {
                bool shouldCloseConnection = transaction == null;

                try
                {
                    if (connection.State != ConnectionState.Open)
                    {
                        connection.Open();
                    }

                    using (IDbCommand command = connection.CreateCommand())
                    {
                        command.Transaction = transaction;
                        command.CommandText = sql;
                        command.CommandTimeout = _config.CommandTimeout;

                        AddUpsertParameters(command, printerState);

                        int rowsAffected = command.ExecuteNonQuery();
                        return rowsAffected > 0;
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
        }

        /// <summary>
        /// Критически важный метод для атомарного резервирования принтера
        /// SELECT FOR UPDATE блокирует строку до окончания транзакции
        /// Это предотвращает race conditions между параллельными процессами
        /// </summary>
        public bool TryReservePrinter(string printerName, string reservedBy, IDbTransaction transaction = null)
        {
            // Этап 1: Блокируем строку и проверяем доступность
            const string selectSql = @"
                SELECT id, printer_name, is_available, version
                FROM printer_states 
                WHERE printer_name = @printerName 
                FOR UPDATE";

            // Этап 2: Атомарно обновляем состояние
            const string updateSql = @"
                UPDATE printer_states 
                SET is_available = false,
                    reserved_by = @reservedBy,
                    reserved_at = @reservedAt,
                    last_updated = @lastUpdated,
                    process_id = @processId,
                    machine_name = @machineName,
                    version = version + 1
                WHERE printer_name = @printerName 
                  AND is_available = true";

            using (IDbConnection connection = transaction?.Connection ?? CreateConnection())
            {
                bool shouldCloseConnection = transaction == null;
                IDbTransaction localTransaction = transaction;

                try
                {
                    if (connection.State != ConnectionState.Open)
                    {
                        connection.Open();
                    }

                    // Создаем транзакцию только если она не передана извне
                    if (localTransaction == null)
                    {
                        localTransaction = connection.BeginTransaction();
                    }

                    bool isAvailable = false;

                    // Этап 1: Проверяем доступность с блокировкой
                    using (IDbCommand selectCommand = connection.CreateCommand())
                    {
                        selectCommand.Transaction = localTransaction;
                        selectCommand.CommandText = selectSql;
                        selectCommand.CommandTimeout = _config.CommandTimeout;

                        IDbDataParameter parameter = selectCommand.CreateParameter();
                        parameter.ParameterName = "@printerName";
                        parameter.Value = printerName;
                        _ = selectCommand.Parameters.Add(parameter);

                        using (IDataReader reader = selectCommand.ExecuteReader())
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
                        {
                            localTransaction.Rollback();
                        }

                        return false;
                    }

                    // Этап 2: Резервируем принтер
                    using (IDbCommand updateCommand = connection.CreateCommand())
                    {
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
                }
                catch (Exception)
                {
                    if (transaction == null && localTransaction != null)
                    {
                        try
                        {
                            localTransaction.Rollback();
                        }
                        catch (Exception ex)
                        {
                            Debug.Fail($"Rollback failed: {ex.Message}");
                        }
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
        }

        /// <summary>
        /// Освобождение принтера - простая операция обновления
        /// Сбрасываем все поля резервирования в исходное состояние
        /// </summary>
        public bool ReleasePrinter(string printerName, IDbTransaction transaction = null)
        {
            const string sql = @"
                UPDATE printer_states 
                SET is_available = true,
                    reserved_by = NULL,
                    reserved_at = NULL,
                    last_updated = @lastUpdated,
                    process_id = NULL,
                    version = version + 1
                WHERE printer_name = @printerName";

            using (IDbConnection connection = transaction?.Connection ?? CreateConnection())
            {
                bool shouldCloseConnection = transaction == null;

                try
                {
                    if (connection.State != ConnectionState.Open)
                    {
                        connection.Open();
                    }

                    using (IDbCommand command = connection.CreateCommand())
                    {
                        command.Transaction = transaction;
                        command.CommandText = sql;
                        command.CommandTimeout = _config.CommandTimeout;

                        IDbDataParameter printerParam = command.CreateParameter();
                        printerParam.ParameterName = "@printerName";
                        printerParam.Value = printerName;
                        _ = command.Parameters.Add(printerParam);

                        IDbDataParameter timeParam = command.CreateParameter();
                        timeParam.ParameterName = "@lastUpdated";
                        timeParam.Value = DateTime.UtcNow;
                        _ = command.Parameters.Add(timeParam);

                        int rowsAffected = command.ExecuteNonQuery();
                        return rowsAffected > 0;
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
        }

        /// <summary>
        /// Очистка зависших резервирований для автоматического восстановления
        /// Важно для случаев аварийного завершения процессов
        /// </summary>
        public int CleanupExpiredReservations(TimeSpan expiredAfter, IDbTransaction transaction = null)
        {
            const string sql = @"
                UPDATE printer_states 
                SET is_available = true,
                    reserved_by = NULL,
                    reserved_at = NULL,
                    last_updated = @lastUpdated,
                    process_id = NULL,
                    version = version + 1
                WHERE is_available = false 
                  AND reserved_at < @expirationTime";

            using (IDbConnection connection = transaction?.Connection ?? CreateConnection())
            {
                bool shouldCloseConnection = transaction == null;

                try
                {
                    if (connection.State != ConnectionState.Open)
                    {
                        connection.Open();
                    }

                    using (IDbCommand command = connection.CreateCommand())
                    {
                        command.Transaction = transaction;
                        command.CommandText = sql;
                        command.CommandTimeout = _config.CommandTimeout;

                        IDbDataParameter timeParam = command.CreateParameter();
                        timeParam.ParameterName = "@lastUpdated";
                        timeParam.Value = DateTime.UtcNow;
                        _ = command.Parameters.Add(timeParam);

                        IDbDataParameter expParam = command.CreateParameter();
                        expParam.ParameterName = "@expirationTime";
                        expParam.Value = DateTime.UtcNow.Subtract(expiredAfter);
                        _ = command.Parameters.Add(expParam);

                        return command.ExecuteNonQuery();
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
        }

        /// <summary>
        /// Инициализация принтеров в базе данных
        /// Используется при первом запуске для заполнения справочника
        /// </summary>
        public void InitializePrinters(IEnumerable<string> printerNames, IDbTransaction transaction = null)
        {
            const string sql = @"
                INSERT INTO printer_states (printer_name, is_available, last_updated, version)
                VALUES (@printerName, true, @lastUpdated, 1)
                ON CONFLICT (printer_name) DO NOTHING";

            using (IDbConnection connection = transaction?.Connection ?? CreateConnection())
            {
                bool shouldCloseConnection = transaction == null;

                try
                {
                    if (connection.State != ConnectionState.Open)
                    {
                        connection.Open();
                    }

                    foreach (string printerName in printerNames)
                    {
                        using (IDbCommand command = connection.CreateCommand())
                        {
                            command.Transaction = transaction;
                            command.CommandText = sql;
                            command.CommandTimeout = _config.CommandTimeout;

                            IDbDataParameter nameParam = command.CreateParameter();
                            nameParam.ParameterName = "@printerName";
                            nameParam.Value = printerName;
                            _ = command.Parameters.Add(nameParam);

                            IDbDataParameter timeParam = command.CreateParameter();
                            timeParam.ParameterName = "@lastUpdated";
                            timeParam.Value = DateTime.UtcNow;
                            _ = command.Parameters.Add(timeParam);

                            _ = command.ExecuteNonQuery();
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
        }

        /// <summary>
        /// Маппинг данных из DataReader в модель
        /// Централизованная логика преобразования для консистентности
        /// </summary>
        private PrinterState MapFromReader(IDataReader reader)
        {
            return new PrinterState
            {
                Id = Convert.ToInt32(reader["id"]),
                PrinterName = reader["printer_name"].ToString(),
                IsAvailable = Convert.ToBoolean(reader["is_available"]),
                ReservedBy = reader["reserved_by"] as string,
                ReservedAt = reader["reserved_at"] as DateTime?,
                LastUpdated = Convert.ToDateTime(reader["last_updated"]),
                ProcessId = reader["process_id"] as int?,
                MachineName = reader["machine_name"] as string,
                Version = Convert.ToInt64(reader["version"])
            };
        }

        /// <summary>
        /// Добавление параметров для Upsert операции
        /// </summary>
        private void AddUpsertParameters(IDbCommand command, PrinterState printerState)
        {
            (string, object)[] parameters = new[]
            {
                ("@printerName", printerState.PrinterName),
                ("@isAvailable", printerState.IsAvailable),
                ("@reservedBy", (object)printerState.ReservedBy ?? DBNull.Value),
                ("@reservedAt", (object)printerState.ReservedAt ?? DBNull.Value),
                ("@lastUpdated", DateTime.UtcNow),
                ("@processId", (object)printerState.ProcessId ?? DBNull.Value),
                ("@machineName", (object)printerState.MachineName ?? DBNull.Value),
                ("@currentVersion", printerState.Version)
            };

            foreach ((string name, object value) in parameters)
            {
                IDbDataParameter param = command.CreateParameter();
                param.ParameterName = name;
                param.Value = value;
                _ = command.Parameters.Add(param);
            }
        }

        /// <summary>
        /// Добавление параметров для резервирования
        /// </summary>
        private void AddReservationParameters(IDbCommand command, string printerName, string reservedBy)
        {
            Process currentProcess = Process.GetCurrentProcess();
            (string, object)[] parameters = new[]
            {
                ("@printerName", printerName),
                ("@reservedBy", reservedBy),
                ("@reservedAt", DateTime.UtcNow),
                ("@lastUpdated", DateTime.UtcNow),
                ("@processId", currentProcess.Id),
                ("@machineName", (object)Environment.MachineName)
            };

            foreach ((string name, object value) in parameters)
            {
                IDbDataParameter param = command.CreateParameter();
                param.ParameterName = name;
                param.Value = value;
                _ = command.Parameters.Add(param);
            }
        }
    }
}