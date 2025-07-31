using Dapper;
using System;
using System.Data.Odbc;

namespace Database.Schema
{
    /// <summary>
    /// Менеджер схемы базы данных, отвечающий исключительно за создание и поддержание
    /// структуры таблиц. Этот класс используется только при первоначальной настройке
    /// или обновлении структуры базы данных.
    /// 
    /// Философия: разделяем ответственность между управлением схемой и бизнес-логикой.
    /// Схема создается один раз администратором, приложение только использует существующие таблицы.
    /// </summary>
    public sealed class SchemaManager : IDisposable
    {
        private readonly string _connectionString;
        private readonly int _commandTimeout;
        private bool _disposed = false;

        public SchemaManager(string connectionString, int commandTimeout = 60)
        {
            _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
            _commandTimeout = commandTimeout;
        }

        /// <summary>
        /// Создает полную схему базы данных для системы управления принтерами.
        /// Этот метод предназначен для однократного выполнения администратором системы.
        /// 
        /// Преимущества такого подхода:
        /// - Полный контроль над структурой таблицы
        /// - Возможность детальной настройки производительности
        /// - Четкое разделение между инфраструктурой и приложением
        /// - Упрощение кода основного сервиса
        /// </summary>
        public void CreatePrinterManagementSchema()
        {
            using OdbcConnection connection = new OdbcConnection(_connectionString);

            connection.Open();

            using OdbcTransaction transaction = connection.BeginTransaction();

            try
            {
                // Создаем основную таблицу принтеров
                CreatePrinterStatesTable(connection, transaction);

                transaction.Commit();

                Console.WriteLine("✓ Схема базы данных для управления принтерами успешно создана");
            }
            catch
            {
                transaction.Rollback();
                throw;
            }
        }

        /// <summary>
        /// Создает основную таблицу принтеров с полными ограничениями целостности.
        /// Использует чистый PostgreSQL синтаксис без оглядки на совместимость с IDE.
        /// </summary>
        private void CreatePrinterStatesTable(OdbcConnection connection, OdbcTransaction transaction)
        {
            const string createTableSql = @"
                -- Основная таблица для отслеживания состояния принтеров
                CREATE TABLE printer_states (
                    -- Первичный ключ с автоинкрементом
                    id SERIAL PRIMARY KEY,
                    
                    -- Уникальное имя принтера в системе
                    printer_name VARCHAR(200) NOT NULL,
                    
                    -- Флаг доступности принтера для резервирования
                    is_available BOOLEAN NOT NULL DEFAULT true,
                    
                    -- Информация о текущем резервировании
                    reserved_by_file VARCHAR(500),
                    reserved_at TIMESTAMPTZ,
                    process_id INTEGER,
                    
                    -- Токен для оптимистичного блокирования
                    change_token UUID NOT NULL DEFAULT gen_random_uuid(),
                    
                    -- Временные метки для аудита (добавляем для полноты)
                    created_at TIMESTAMPTZ DEFAULT CURRENT_TIMESTAMP,
                    updated_at TIMESTAMPTZ DEFAULT CURRENT_TIMESTAMP
                );";

            _ = connection.Execute(createTableSql, transaction: transaction, commandTimeout: _commandTimeout);

            // Добавляем ограничения отдельными командами для лучшей читаемости
            AddTableConstraints(connection, transaction);
        }

        /// <summary>
        /// Добавляет ограничения целостности данных к таблице принтеров.
        /// Эти ограничения обеспечивают логическую консистентность на уровне базы данных.
        /// </summary>
        private void AddTableConstraints(OdbcConnection connection, OdbcTransaction transaction)
        {
            string[] constraints = {
                // Уникальность имени принтера
                "ALTER TABLE printer_states ADD CONSTRAINT uk_printer_name UNIQUE (printer_name);",
                
                // Валидация имени принтера
                "ALTER TABLE printer_states ADD CONSTRAINT chk_printer_name_valid CHECK (LENGTH(TRIM(printer_name)) > 0);",
                
                // Валидация пути к файлу
                "ALTER TABLE printer_states ADD CONSTRAINT chk_file_path_valid CHECK (reserved_by_file IS NULL OR LENGTH(TRIM(reserved_by_file)) > 0);",
                
                // Логическая целостность резервирования
                @"ALTER TABLE printer_states ADD CONSTRAINT chk_reservation_logic CHECK (
                    (is_available = true AND reserved_by_file IS NULL AND reserved_at IS NULL AND process_id IS NULL) OR
                    (is_available = false AND reserved_by_file IS NOT NULL AND reserved_at IS NOT NULL AND process_id IS NOT NULL)
                );"
            };

            foreach (string constraint in constraints)
            {
                _ = connection.Execute(constraint, transaction: transaction, commandTimeout: _commandTimeout);
            }
        }

        /// <summary>
        /// Проверяет существование и корректность схемы базы данных.
        /// Полезно для диагностики проблем развертывания.
        /// </summary>
        public bool ValidateSchema()
        {
            try
            {
                using OdbcConnection connection = new OdbcConnection(_connectionString);
                connection.Open();

                // Проверяем существование таблицы
                const string checkTableSql = @"
                    SELECT COUNT(*) 
                    FROM information_schema.tables 
                    WHERE table_name = 'printer_states' AND table_type = 'BASE TABLE';";

                int tableCount = connection.QuerySingle<int>(checkTableSql, commandTimeout: _commandTimeout);

                if (tableCount == 0)
                {
                    Console.WriteLine("✗ Таблица printer_states не найдена");
                    return false;
                }

                // Проверяем наличие ключевых столбцов
                const string checkColumnsSql = @"
                    SELECT COUNT(*) 
                    FROM information_schema.columns 
                    WHERE table_name = 'printer_states' 
                    AND column_name IN ('id', 'printer_name', 'is_available', 'change_token');";

                int columnCount = connection.QuerySingle<int>(checkColumnsSql, commandTimeout: _commandTimeout);

                if (columnCount < 4)
                {
                    Console.WriteLine("✗ В таблице printer_states отсутствуют необходимые столбцы");
                    return false;
                }

                Console.WriteLine("✓ Схема базы данных валидна и готова к использованию");
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"✗ Ошибка при валидации схемы: {ex.Message}");
                return false;
            }
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