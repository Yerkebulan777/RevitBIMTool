using Database.Configuration;
using Database.Models;
using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;

namespace Database.Repositories
{
    /// <summary>
    /// Полная реализация репозитория для работы с принтерами
    /// Ключевой принцип: все методы работают через универсальные ADO.NET интерфейсы,
    /// что позволяет использовать любую базу данных без изменения кода
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
        /// Этот метод - ключ к универсальности: провайдер знает, 
        /// как создать правильное подключение для конкретной СУБД
        /// </summary>
        private IDbConnection CreateConnection()
        {
            return _config.Provider.CreateConnection(_config.ConnectionString);
        }

        /// <summary>
        /// Получение принтера по имени
        /// Простой SELECT запрос, работающий одинаково во всех СУБД
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

                        IDbDataParameter parameter = CreateParameter(command, "@printerName", printerName);
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
        /// Фильтрация происходит на уровне базы данных для максимальной производительности
        /// WHERE is_available = 1 использует индекс для быстрого поиска
        /// </summary>
        public IEnumerable<PrinterState> GetAvailablePrinters(IDbTransaction transaction = null)
        {
            const string sql = @"
                SELECT id, printer_name, is_available, reserved_by, reserved_at, 
                       last_updated, process_id, machine_name, version
                FROM printer_states 
                WHERE is_available = 1 
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
        /// Upsert операция - создание или обновление принтера
        /// Разные СУБД поддерживают upsert по-разному:
        /// - SQLite: INSERT OR REPLACE
        /// - SQL Server: MERGE или IF EXISTS
        /// - PostgreSQL: ON CONFLICT DO UPDATE
        /// Здесь мы используем простой подход: сначала пробуем UPDATE, потом INSERT
        /// </summary>
        public bool UpsertPrinter(PrinterState printerState, IDbTransaction transaction = null)
        {
            // Сначала пробуем обновить существующую запись
            const string updateSql = @"
                UPDATE printer_states 
                SET is_available = @isAvailable,
                    reserved_by = @reservedBy,
                    reserved_at = @reservedAt,
                    last_updated = @lastUpdated,
                    process_id = @processId,
                    machine_name = @machineName,
                    version = version + 1
                WHERE printer_name = @printerName";

            // Если обновление не сработало, вставляем новую запись
            const string insertSql = @"
                INSERT INTO printer_states 
                    (printer_name, is_available, reserved_by, reserved_at, last_updated, process_id, machine_name, version)
                VALUES 
                    (@printerName, @isAvailable, @reservedBy, @reservedAt, @lastUpdated, @processId, @machineName, 1)";

            using (IDbConnection connection = transaction?.Connection ?? CreateConnection())
            {
                bool shouldCloseConnection = transaction == null;

                try
                {
                    if (connection.State != ConnectionState.Open)
                    {
                        connection.Open();
                    }

                    // Пробуем UPDATE
                    using (IDbCommand updateCommand = connection.CreateCommand())
                    {
                        updateCommand.Transaction = transaction;
                        updateCommand.CommandText = updateSql;
                        updateCommand.CommandTimeout = _config.CommandTimeout;

                        AddUpsertParameters(updateCommand, printerState);

                        int rowsAffected = updateCommand.ExecuteNonQuery();
                        if (rowsAffected > 0)
                        {
                            return true; // Обновление прошло успешно
                        }
                    }

                    // Если UPDATE не сработал, делаем INSERT
                    using (IDbCommand insertCommand = connection.CreateCommand())
                    {
                        insertCommand.Transaction = transaction;
                        insertCommand.CommandText = insertSql;
                        insertCommand.CommandTimeout = _config.CommandTimeout;

                        AddUpsertParameters(insertCommand, printerState);

                        int rowsAffected = insertCommand.ExecuteNonQuery();
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
        /// Ключевой метод для атомарного резервирования принтера
        /// Это самая сложная операция, требующая правильной работы с блокировками
        /// Алгоритм:
        /// 1. Начинаем транзакцию (если не передана извне)
        /// 2. Блокируем строку принтера (если СУБД поддерживает)
        /// 3. Проверяем доступность
        /// 4. Если доступен - обновляем статус на "зарезервирован"
        /// 5. Коммитим или откатываем транзакцию
        /// </summary>
        public bool TryReservePrinter(string printerName, string reservedBy, IDbTransaction transaction = null)
        {
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

                    // Этап 1: Получаем SQL для блокировки, специфичный для данной СУБД
                    string selectSql = _config.Provider.GetReservePrinterScript();
                    bool isAvailable = false;

                    // Этап 2: Проверяем доступность с блокировкой строки
                    using (IDbCommand selectCommand = connection.CreateCommand())
                    {
                        selectCommand.Transaction = localTransaction;
                        selectCommand.CommandText = selectSql;
                        selectCommand.CommandTimeout = _config.CommandTimeout;

                        IDbDataParameter parameter = CreateParameter(selectCommand, "@printerName", printerName);
                        _ = selectCommand.Parameters.Add(parameter);

                        using (IDataReader reader = selectCommand.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                // В разных СУБД тип поля is_available может быть разным
                                // SQLite хранит как INTEGER (0/1), SQL Server как BIT
                                object availableValue = reader["is_available"];
                                isAvailable = Convert.ToBoolean(availableValue);
                            }
                        }
                    }

                    // Если принтер недоступен, откатываем и выходим
                    if (!isAvailable)
                    {
                        if (transaction == null)
                        {
                            localTransaction.Rollback();
                        }

                        return false;
                    }

                    // Этап 3: Резервируем принтер атомарно
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

                    using (IDbCommand updateCommand = connection.CreateCommand())
                    {
                        updateCommand.Transaction = localTransaction;
                        updateCommand.CommandText = updateSql;
                        updateCommand.CommandTimeout = _config.CommandTimeout;

                        AddReservationParameters(updateCommand, printerName, reservedBy);

                        int rowsAffected = updateCommand.ExecuteNonQuery();

                        if (rowsAffected > 0)
                        {
                            // Успешно зарезервировали
                            if (transaction == null)
                            {
                                localTransaction.Commit();
                            }

                            return true;
                        }
                        else
                        {
                            // Кто-то другой успел зарезервировать между нашей проверкой и обновлением
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
                    // При любой ошибке откатываем транзакцию
                    if (transaction == null && localTransaction != null)
                    {
                        try 
                        { 
                            localTransaction.Rollback(); 
                        } 
                        catch (Exception ex)
                        { 
                            Debug.Fail(ex.Message); 
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
        /// Освобождение принтера
        /// Простая операция UPDATE - сбрасываем все поля резервирования
        /// Операция идемпотентная - можно вызывать многократно без вреда
        /// </summary>
        public bool ReleasePrinter(string printerName, IDbTransaction transaction = null)
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

                        IDbDataParameter printerParam = CreateParameter(command, "@printerName", printerName);
                        _ = command.Parameters.Add(printerParam);

                        IDbDataParameter timeParam = CreateParameter(command, "@lastUpdated", FormatDateTime(DateTime.UtcNow));
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
        /// Очистка зависших резервирований
        /// Важная функция для восстановления системы после аварийных завершений процессов
        /// Освобождает принтеры, которые были зарезервированы дольше указанного времени
        /// </summary>
        public int CleanupExpiredReservations(TimeSpan expiredAfter, IDbTransaction transaction = null)
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
                WHERE is_available = 0 
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

                        IDbDataParameter timeParam = CreateParameter(command, "@lastUpdated", FormatDateTime(DateTime.UtcNow));
                        _ = command.Parameters.Add(timeParam);

                        IDbDataParameter expParam = CreateParameter(command, "@expirationTime", FormatDateTime(DateTime.UtcNow.Subtract(expiredAfter)));
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
        /// Создает записи для всех известных принтеров при первом запуске системы
        /// Использует INSERT OR IGNORE логику - если принтер уже есть, пропускаем
        /// </summary>
        public void InitializePrinters(IEnumerable<string> printerNames, IDbTransaction transaction = null)
        {
            // Используем простейший подход: для каждого принтера пробуем INSERT
            // Если принтер уже существует, операция просто не выполнится
            const string sql = @"
                INSERT INTO printer_states (printer_name, is_available, last_updated, version)
                SELECT @printerName, 1, @lastUpdated, 1
                WHERE NOT EXISTS (
                    SELECT 1 FROM printer_states WHERE printer_name = @printerName
                )";

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

                            IDbDataParameter nameParam = CreateParameter(command, "@printerName", printerName);
                            _ = command.Parameters.Add(nameParam);

                            IDbDataParameter timeParam = CreateParameter(command, "@lastUpdated", FormatDateTime(DateTime.UtcNow));
                            _ = command.Parameters.Add(timeParam);

                            try
                            {
                                _ = command.ExecuteNonQuery();
                            }
                            catch (Exception)
                            {
                                // Игнорируем ошибки дублирования - принтер уже существует
                                // В разных СУБД ошибки дублирования имеют разные коды
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
        }

        #region Вспомогательные методы

        /// <summary>
        /// Универсальное создание параметра команды
        /// Каждая СУБД создает свои типы параметров, но все они реализуют IDbDataParameter
        /// </summary>
        private IDbDataParameter CreateParameter(IDbCommand command, string name, object value)
        {
            IDbDataParameter parameter = command.CreateParameter();
            parameter.ParameterName = name;
            parameter.Value = value ?? DBNull.Value;
            return parameter;
        }

        /// <summary>
        /// Маппинг данных из DataReader в объект модели
        /// Здесь важно правильно обрабатывать различия в типах данных между СУБД
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
        /// Разные СУБД могут хранить даты как DateTime, string или другие типы
        /// </summary>
        private DateTime? ParseDateTime(object value)
        {
            if (value == null || value == DBNull.Value)
            {
                return null;
            }

            if (value is DateTime dateTime)
            {
                return dateTime;
            }

            return value is string dateString && DateTime.TryParse(dateString, out DateTime parsed) ? parsed : (DateTime?)null;
        }

        /// <summary>
        /// Форматирование даты в универсальный строковый формат
        /// Некоторые СУБД лучше работают со строковым представлением дат
        /// </summary>
        private string FormatDateTime(DateTime dateTime)
        {
            return dateTime.ToString("yyyy-MM-dd HH:mm:ss");
        }

        /// <summary>
        /// Добавление параметров для Upsert операции
        /// Централизованная логика добавления параметров обеспечивает консистентность
        /// </summary>
        private void AddUpsertParameters(IDbCommand command, PrinterState printerState)
        {
            _ = command.Parameters.Add(CreateParameter(command, "@printerName", printerState.PrinterName));
            _ = command.Parameters.Add(CreateParameter(command, "@isAvailable", printerState.IsAvailable ? 1 : 0));
            _ = command.Parameters.Add(CreateParameter(command, "@reservedBy", printerState.ReservedBy));
            _ = command.Parameters.Add(CreateParameter(command, "@reservedAt",
                printerState.ReservedAt?.ToString("yyyy-MM-dd HH:mm:ss")));
            _ = command.Parameters.Add(CreateParameter(command, "@lastUpdated", FormatDateTime(DateTime.UtcNow)));
            _ = command.Parameters.Add(CreateParameter(command, "@processId", printerState.ProcessId));
            _ = command.Parameters.Add(CreateParameter(command, "@machineName", printerState.MachineName));
        }

        /// <summary>
        /// Добавление параметров для резервирования принтера
        /// Автоматически заполняет все необходимые поля текущими значениями процесса
        /// </summary>
        private void AddReservationParameters(IDbCommand command, string printerName, string reservedBy)
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