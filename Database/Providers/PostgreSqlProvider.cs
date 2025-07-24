using System.Data;

namespace Database.Providers
{
    /// <summary>
    /// Абстрактный PostgreSQL провайдер для .NET Standard 2.0
    /// Основной проект RevitBIMTool переопределит CreateConnection с реальным NpgsqlConnection
    /// </summary>
    public abstract class PostgreSqlProvider : IDatabaseProvider
    {
        public string ProviderName => "PostgreSQL";
        public bool SupportsRowLevelLocking => true;

        /// <summary>
        /// Абстрактный метод - будет переопределен в RevitBIMTool
        /// там будет: return new NpgsqlConnection(connectionString);
        /// </summary>
        public abstract IDbConnection CreateConnection(string connectionString);

        public string GetCreateTableScript()
        {
            return @"
                    CREATE TABLE IF NOT EXISTS printer_states (
                        id SERIAL PRIMARY KEY,
                        printer_name VARCHAR(100) NOT NULL UNIQUE,
                        is_available BOOLEAN NOT NULL DEFAULT true,
                        reserved_by VARCHAR(100) NULL,
                        reserved_at TIMESTAMP WITH TIME ZONE NULL,
                        last_updated TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW(),
                        process_id INTEGER NULL,
                        machine_name VARCHAR(50) NULL,
                        version BIGINT NOT NULL DEFAULT 1
                    );
                    
                    CREATE INDEX IF NOT EXISTS idx_printer_states_available 
                        ON printer_states (is_available) WHERE is_available = true;";
        }

        /// <summary>
        /// Критически важный SQL для атомарного резервирования
        /// FOR UPDATE блокирует строку до конца транзакции
        /// </summary>
        public string GetReservePrinterScript()
        {
            return @"
                    SELECT id, printer_name, is_available, version
                    FROM printer_states 
                    WHERE printer_name = @printerName 
                      AND is_available = true
                    FOR UPDATE";
        }

        /// <summary>
        /// SQL для атомарного обновления с версионированием
        /// Версионирование предотвращает потерянные обновления
        /// </summary>
        public string GetUpdateReservationScript()
        {
            return @"
                UPDATE printer_states 
                SET is_available = false,
                    reserved_by = @reservedBy,
                    reserved_at = NOW(),
                    last_updated = NOW(),
                    process_id = @processId,
                    machine_name = @machineName,
                    version = version + 1
                WHERE printer_name = @printerName 
                  AND is_available = true
                  AND version = @expectedVersion";
        }

        public virtual void Initialize(string connectionString)
        {
            // Базовая инициализация без создания подключения
            // Конкретная реализация выполнит CREATE TABLE
        }
    }
}