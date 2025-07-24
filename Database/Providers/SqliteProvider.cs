// Database/Providers/SqliteProvider.cs
using Database.Providers;
using System;
using System.Data;
using System.Data.SQLite;

namespace  Database.Providers
{
    /// <summary>
    /// Провайдер для SQLite - идеальный выбор для небольших приложений
    /// SQLite работает везде, не требует установки сервера, очень быстрый для наших задач
    /// Это встроенная база данных, которая хранится в одном файле
    /// </summary>
    public class SqliteProvider : IDatabaseProvider
    {
        public string ProviderName => "SQLite";

        /// <summary>
        /// SQLite не поддерживает строковые блокировки, но транзакции работают отлично
        /// Для наших задач этого более чем достаточно
        /// </summary>
        public bool SupportsRowLevelLocking => false;

        /// <summary>
        /// Создание подключения к SQLite
        /// Если файла базы данных не существует, SQLite создаст его автоматически
        /// </summary>
        public IDbConnection CreateConnection(string connectionString)
        {
            // SQLite подключается напрямую к файлу базы данных
            return new SQLiteConnection(connectionString);
        }

        /// <summary>
        /// SQL-скрипт для создания таблицы в SQLite
        /// SQLite имеет более простой синтаксис по сравнению с PostgreSQL
        /// </summary>
        public string GetCreateTableScript()
        {
            return @"
                CREATE TABLE IF NOT EXISTS printer_states (
                    id INTEGER PRIMARY KEY AUTOINCREMENT,
                    printer_name TEXT NOT NULL UNIQUE,
                    is_available INTEGER NOT NULL DEFAULT 1,
                    reserved_by TEXT NULL,
                    reserved_at TEXT NULL,
                    last_updated TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP,
                    process_id INTEGER NULL,
                    machine_name TEXT NULL,
                    version INTEGER NOT NULL DEFAULT 1
                );
                
                CREATE INDEX IF NOT EXISTS idx_printer_states_available 
                    ON printer_states (is_available) WHERE is_available = 1;
                    
                CREATE INDEX IF NOT EXISTS idx_printer_states_reserved_at 
                    ON printer_states (reserved_at) WHERE reserved_at IS NOT NULL;";
        }

        /// <summary>
        /// Для SQLite используем простой SELECT без блокировок
        /// Транзакции в SQLite по умолчанию сериализуемые, что обеспечивает безопасность
        /// </summary>
        public string GetReservePrinterScript()
        {
            return @"
                SELECT id, printer_name, is_available, version
                FROM printer_states 
                WHERE printer_name = @printerName AND is_available = 1";
        }
    }
}