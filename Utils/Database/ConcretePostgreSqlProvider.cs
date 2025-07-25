using Database.Providers;
using Npgsql;
using System.Data;

namespace RevitBIMTool.Utils.Database
{
    /// <summary>
    /// Конкретная реализация PostgreSQL провайдера для основного проекта
    /// </summary>
    public class ConcretePostgreSqlProvider : IDatabaseProvider
    {
        public string ProviderName => "PostgreSQL";
        public bool SupportsRowLevelLocking => true;

        public IDbConnection CreateConnection(string connectionString)
        {
            return new NpgsqlConnection(connectionString);
        }

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

        public string GetReservePrinterScript()
        {
            return @"
                SELECT id, printer_name, is_available, version
                FROM printer_states 
                WHERE printer_name = @printerName AND is_available = true
                FOR UPDATE";
        }

        public void Initialize(string connectionString)
        {
            using var connection = CreateConnection(connectionString);
            connection.Open();

            using var command = connection.CreateCommand();
            command.CommandText = GetCreateTableScript();
            command.ExecuteNonQuery();
        }
    }
}