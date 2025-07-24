using System;
using System.Data;
using System.Data.SqlClient;

namespace Database.Providers
{
    /// <summary>
    /// Провайдер для Microsoft SQL Server
    /// Подходит для корпоративных сред, где уже есть SQL Server
    /// Поддерживает все современные возможности блокировок и транзакций
    /// </summary>
    public class SqlServerProvider : IDatabaseProvider
    {
        public string ProviderName => "SQL Server";

        /// <summary>
        /// SQL Server отлично поддерживает строковые блокировки
        /// Это позволяет нам использовать оптимальные стратегии блокировки
        /// </summary>
        public bool SupportsRowLevelLocking => true;

        public IDbConnection CreateConnection(string connectionString)
        {
            return new SqlConnection(connectionString);
        }

        /// <summary>
        /// SQL-скрипт для создания таблицы в SQL Server
        /// Используем современный синтаксис T-SQL с оптимизациями
        /// </summary>
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
                        ON printer_states (is_available) 
                        WHERE is_available = 1;
                        
                    CREATE NONCLUSTERED INDEX idx_printer_states_reserved_at 
                        ON printer_states (reserved_at) 
                        WHERE reserved_at IS NOT NULL;
                END";
        }

        /// <summary>
        /// SQL Server поддерживает различные уровни блокировок
        /// UPDLOCK + ROWLOCK обеспечивают точечную блокировку нужной строки
        /// </summary>
        public string GetReservePrinterScript()
        {
            return @"
                SELECT id, printer_name, is_available, version
                FROM printer_states WITH (UPDLOCK, ROWLOCK)
                WHERE printer_name = @printerName AND is_available = 1";
        }
    }
}