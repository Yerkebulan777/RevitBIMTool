using System;
using System.Data;

namespace Database.Providers
{
    /// <summary>
    /// Абстрактный SQL Server провайдер для демонстрации принципов
    /// Показывает, как должен работать провайдер для корпоративной СУБД
    /// </summary>
    public class SqlServerProvider : IDatabaseProvider
    {
        public string ProviderName => "SQL Server";
        public bool SupportsRowLevelLocking => true;

        /// <summary>
        /// В реальной реализации здесь будет: new SqlConnection(connectionString)
        /// </summary>
        public virtual IDbConnection CreateConnection(string connectionString)
        {
            throw new NotImplementedException(
                "SqlServerProvider is abstract. Override CreateConnection in your main project with real SqlConnection.");
        }

        public string GetCreateTableScript()
        {
            return @"
                IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'printer_states')
                BEGIN
                    CREATE TABLE printer_states (
                        id INT IDENTITY(1,1) PRIMARY KEY,
                        printer_name NVARCHAR(100) NOT NULL UNIQUE,
                        is_available BIT NOT NULL DEFAULT 1,
                        reserved_by NVARCHAR(100) NULL,
                        reserved_at DATETIME2 NULL,
                        last_updated DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
                        process_id INT NULL,
                        machine_name NVARCHAR(50) NULL,
                        version BIGINT NOT NULL DEFAULT 1
                    );
                    
                    CREATE NONCLUSTERED INDEX idx_printer_states_available 
                        ON printer_states (is_available) WHERE is_available = 1;
                END";
        }

        public string GetReservePrinterScript()
        {
            return @"
                SELECT id, printer_name, is_available, version
                FROM printer_states WITH (UPDLOCK, ROWLOCK)
                WHERE printer_name = @printerName AND is_available = 1";
        }

        /// <summary>
        /// Инициализация SQL Server провайдера
        /// Обычно включает проверку подключения и создание схемы
        /// </summary>
        public virtual void Initialize(string connectionString)
        {
            // В реальной реализации здесь будет:
            // 1. Проверка подключения к SQL Server
            // 2. Создание базы данных если не существует  
            // 3. Выполнение CREATE TABLE скрипта
        }
    }
}