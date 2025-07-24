using System;
using System.Data;

namespace Database.Providers
{
    /// <summary>
    /// Абстрактный SQLite провайдер для демонстрации принципов
    /// В реальном проекте RevitBIMTool этот провайдер будет переопределен
    /// с использованием настоящего System.Data.SQLite
    /// 
    /// Этот класс показывает КОНТРАКТ того, как должен работать SQLite провайдер
    /// </summary>
    public class SqliteProvider : IDatabaseProvider
    {
        public string ProviderName => "SQLite";
        public bool SupportsRowLevelLocking => false;

        /// <summary>
        /// В реальной реализации здесь будет: new SQLiteConnection(connectionString)
        /// Пока возвращаем заглушку для демонстрации архитектуры
        /// </summary>
        public virtual IDbConnection CreateConnection(string connectionString)
        {
            // Это заглушка! В реальном проекте RevitBIMTool вы переопределите этот метод
            throw new NotImplementedException(
                "SQLiteProvider is abstract. Override CreateConnection in your main project with real SQLite implementation.");
        }

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
                    ON printer_states (is_available) WHERE is_available = 1;";
        }

        public string GetReservePrinterScript()
        {
            return @"
                SELECT id, printer_name, is_available, version
                FROM printer_states 
                WHERE printer_name = @printerName AND is_available = 1";
        }

        /// <summary>
        /// Инициализация провайдера - добавляем этот метод
        /// В SQLite обычно требуется создать файл базы данных если его нет
        /// </summary>
        public virtual void Initialize(string connectionString)
        {
            // В реальной реализации здесь будет логика создания файла базы данных
            // и выполнения CREATE TABLE скрипта
        }
    }
}