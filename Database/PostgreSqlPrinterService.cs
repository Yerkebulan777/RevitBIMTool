using Dapper;
using Database.Models;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Odbc;
using System.Diagnostics;
using System.Linq;

namespace Database
{
    /// <summary>
    /// Прямая работа с PostgreSQL через ODBC и Dapper
    /// Никаких абстракций - только реальная бизнес-логика
    /// </summary>
    public class PostgreSqlPrinterService : IDisposable
    {
        private readonly string _connectionString;
        private readonly int _commandTimeout;
        private bool _disposed = false;

        // SQL скрипты вынесены в константы для лучшей читаемости
        private const string CreateTableSql = @"
            CREATE TABLE IF NOT EXISTS printer_states (
                id SERIAL PRIMARY KEY,
                printer_name VARCHAR(100) NOT NULL UNIQUE,
                is_available BOOLEAN NOT NULL DEFAULT true,
                reserved_by VARCHAR(100) NULL,
                reserved_at TIMESTAMP WITH TIME ZONE NULL,
                version BIGINT NOT NULL DEFAULT 1,
                process_id INTEGER NULL
            );
            
            CREATE INDEX IF NOT EXISTS idx_printer_states_available 
                ON printer_states (is_available) WHERE is_available = true;";

        private const string SelectForUpdateSql = @"
            SELECT id, printer_name, is_available, version
            FROM printer_states 
            WHERE printer_name = @printerName AND is_available = true
            FOR UPDATE";

        private const string ReservePrinterSql = @"
            UPDATE printer_states 
            SET is_available = false,
                reserved_by = @reservedBy,
                reserved_at = @reservedAt,
                process_id = @processId,
                version = version + 1
            WHERE printer_name = @printerName 
              AND is_available = true 
              AND version = @expectedVersion";

        private const string ReleasePrinterSql = @"
            UPDATE printer_states 
            SET is_available = true,
                reserved_by = NULL,
                reserved_at = NULL,
                process_id = NULL,
                version = version + 1
            WHERE printer_name = @printerName";

        public PostgreSqlPrinterService(string connectionString, int commandTimeout = 30)
        {
            _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
            _commandTimeout = commandTimeout;
            // Инициализируем БД
            InitializeDatabase();
        }

        /// <summary>
        /// Создает таблицы и индексы если их нет
        /// Выполняется один раз при инициализации
        /// </summary>
        private void InitializeDatabase()
        {
            using OdbcConnection connection = new OdbcConnection(_connectionString);
            connection.Open();

            // Dapper делает всю работу за нас - никаких команд и параметров
            _ = connection.Execute(CreateTableSql, commandTimeout: _commandTimeout);
        }

        /// <summary>
        /// Инициализирует список принтеров в системе
        /// Добавляет принтеры, которых еще нет в БД
        /// </summary>
        public void InitializePrinters(params string[] printerNames)
        {
            if (printerNames == null || printerNames.Length == 0)
            {
                return;
            }

            using OdbcConnection connection = new OdbcConnection(_connectionString);
            connection.Open();

            const string insertSql = @"
                INSERT INTO printer_states (printer_name, is_available, version)
                VALUES (@printerName, true, 1)
                ON CONFLICT (printer_name) DO NOTHING";

            // Dapper автоматически обрабатывает массив параметров
            var parameters = printerNames.Select(name => new { printerName = name });
            _ = connection.Execute(insertSql, parameters, commandTimeout: _commandTimeout);
        }

        /// <summary>
        /// Пытается зарезервировать любой доступный принтер
        /// Возвращает имя зарезервированного принтера или null
        /// </summary>
        public string TryReserveAnyAvailablePrinter(string reservedBy, params string[] preferredPrinters)
        {
            if (string.IsNullOrEmpty(reservedBy))
            {
                throw new ArgumentException("ReservedBy cannot be null or empty", nameof(reservedBy));
            }

            using OdbcConnection connection = new OdbcConnection(_connectionString);
            connection.Open();

            // Получаем список доступных принтеров
            IEnumerable<PrinterState> availablePrinters = GetAvailablePrinters(connection);

            if (!availablePrinters.Any())
            {
                return null;
            }

            // Упорядочиваем по приоритету: сначала предпочтительные, потом все остальные
            IEnumerable<PrinterState> orderedPrinters = OrderByPreference(availablePrinters, preferredPrinters);

            // Пытаемся зарезервировать первый доступный
            PrinterState reservedPrinter = orderedPrinters.FirstOrDefault(printer => TryReserveSpecificPrinter(connection, printer.PrinterName, reservedBy));

            return reservedPrinter?.PrinterName;
        }

        /// <summary>
        /// Резервирует конкретный принтер
        /// Использует оптимистичную блокировку для предотвращения race conditions
        /// </summary>
        public bool TryReserveSpecificPrinter(string printerName, string reservedBy)
        {
            if (string.IsNullOrEmpty(printerName))
            {
                throw new ArgumentException("PrinterName cannot be null or empty", nameof(printerName));
            }

            if (string.IsNullOrEmpty(reservedBy))
            {
                throw new ArgumentException("ReservedBy cannot be null or empty", nameof(reservedBy));
            }

            using OdbcConnection connection = new OdbcConnection(_connectionString);
            connection.Open();

            return TryReserveSpecificPrinter(connection, printerName, reservedBy);
        }

        /// <summary>
        /// Внутренняя реализация резервирования с использованием существующего соединения
        /// Показывает реальную работу с транзакциями и блокировками
        /// </summary>
        private bool TryReserveSpecificPrinter(IDbConnection connection, string printerName, string reservedBy)
        {
            using IDbTransaction transaction = connection.BeginTransaction();
            try
            {
                // Шаг 1: Блокируем строку для чтения (пессимистичная блокировка)
                // SELECT FOR UPDATE гарантирует, что никто другой не сможет изменить эту строку
                (long version, bool isAvailable) = connection.QuerySingleOrDefault<(long version, bool isAvailable)>(
                    SelectForUpdateSql,
                    new { printerName },
                    transaction,
                    _commandTimeout);

                // Проверяем, найден ли принтер и доступен ли он
                if (version == 0 || !isAvailable)
                {
                    transaction.Rollback();
                    return false;
                }

                // Шаг 2: Обновляем состояние принтера (оптимистичная блокировка)
                // Проверяем version чтобы убедиться, что никто не изменил запись между SELECT и UPDATE
                Process currentProcess = Process.GetCurrentProcess();
                int affectedRows = connection.Execute(
                    ReservePrinterSql,
                    new
                    {
                        printerName,
                        reservedBy,
                        reservedAt = DateTime.UtcNow,
                        processId = currentProcess.Id,
                        expectedVersion = version
                    },
                    transaction,
                    _commandTimeout);

                if (affectedRows > 0)
                {
                    transaction.Commit();
                    return true;
                }
                else
                {
                    // Кто-то успел изменить принтер между нашим SELECT и UPDATE
                    transaction.Rollback();
                    return false;
                }
            }
            catch
            {
                transaction.Rollback();
                throw;
            }
        }

        /// <summary>
        /// Освобождает зарезервированный принтер
        /// Простая операция без сложной логики блокировок
        /// </summary>
        public bool ReleasePrinter(string printerName)
        {
            if (string.IsNullOrEmpty(printerName))
            {
                throw new ArgumentException("PrinterName cannot be null or empty", nameof(printerName));
            }

            using OdbcConnection connection = new OdbcConnection(_connectionString);
            connection.Open();

            int affectedRows = connection.Execute(
                ReleasePrinterSql,
                new { printerName },
                commandTimeout: _commandTimeout);

            return affectedRows > 0;
        }

        /// <summary>
        /// Получает список всех доступных принтеров
        /// Простой SELECT без блокировок
        /// </summary>
        public IEnumerable<PrinterState> GetAvailablePrinters()
        {
            using OdbcConnection connection = new OdbcConnection(_connectionString);
            connection.Open();

            return GetAvailablePrinters(connection);
        }

        /// <summary>
        /// Внутренняя реализация получения доступных принтеров
        /// </summary>
        private IEnumerable<PrinterState> GetAvailablePrinters(IDbConnection connection)
        {
            const string sql = @"
                SELECT id, printer_name, is_available, reserved_by, reserved_at, version, process_id
                FROM printer_states 
                WHERE is_available = true 
                ORDER BY printer_name";

            // Dapper автоматически маппит результат на PrinterState
            return connection.Query<PrinterState>(sql, commandTimeout: _commandTimeout);
        }

        /// <summary>
        /// Получает полную информацию о всех принтерах
        /// Для мониторинга и диагностики
        /// </summary>
        public IEnumerable<PrinterState> GetAllPrinters()
        {
            using OdbcConnection connection = new OdbcConnection(_connectionString);
            connection.Open();

            const string sql = @"
                SELECT id, printer_name, is_available, reserved_by, reserved_at, version, process_id
                FROM printer_states 
                ORDER BY printer_name";

            return connection.Query<PrinterState>(sql, commandTimeout: _commandTimeout);
        }

        /// <summary>
        /// Очищает зависшие резервирования
        /// Важная функция для поддержания системы в рабочем состоянии
        /// </summary>
        public int CleanupExpiredReservations(TimeSpan maxAge)
        {
            using OdbcConnection connection = new OdbcConnection(_connectionString);
            connection.Open();

            DateTime cutoffTime = DateTime.UtcNow.Subtract(maxAge);

            const string sql = @"
                UPDATE printer_states 
                SET is_available = true,
                    reserved_by = NULL,
                    reserved_at = NULL,
                    process_id = NULL,
                    version = version + 1
                WHERE is_available = false 
                  AND reserved_at < @cutoffTime";

            return connection.Execute(sql, new { cutoffTime }, commandTimeout: _commandTimeout);
        }

        /// <summary>
        /// Упорядочивает принтеры по предпочтениям
        /// Предпочтительные принтеры идут первыми
        /// </summary>
        private static IEnumerable<PrinterState> OrderByPreference(IEnumerable<PrinterState> printers, string[] preferredPrinters)
        {
            if (preferredPrinters is null || preferredPrinters.Length == 0)
            {
                return printers.OrderBy(p => p.PrinterName);
            }

            HashSet<string> preferredSet = new HashSet<string>(preferredPrinters, StringComparer.OrdinalIgnoreCase);

            return printers.OrderBy(p => preferredSet.Contains(p.PrinterName) ? 0 : 1).ThenBy(p => p.PrinterName);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                // Если появятся управляемые ресурсы, их освобождение здесь
                // Сейчас освобождать нечего, но шаблон соблюдён

                _disposed = true;
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }



    }
}