using System;
using System.Data;
using Npgsql;

namespace Database.Providers
{
    /// <summary>
    /// PostgreSQL провайдер с оптимизированными блокировками
    /// Использует SELECT FOR UPDATE для атомарного резервирования принтеров
    /// </summary>
    public class PostgreSqlProvider : IDatabaseProvider
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
                    is_available BOOLEAN NOT NULL DEFAULT TRUE,
                    reserved_by VARCHAR(100),
                    reserved_at TIMESTAMP WITH TIME ZONE,
                    last_updated TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW(),
                    process_id INTEGER,
                    machine_name VARCHAR(50),
                    version BIGINT NOT NULL DEFAULT 1
                );
                
                CREATE INDEX IF NOT EXISTS idx_printer_states_available 
                    ON printer_states (printer_name) WHERE is_available = TRUE;";
        }

        public string GetReservePrinterScript()
        {
            // SELECT FOR UPDATE блокирует строку до конца транзакции
            // NOWAIT возвращает ошибку вместо ожидания, если строка заблокирована
            return @"
                SELECT id, printer_name, is_available, version
                FROM printer_states 
                WHERE printer_name = @printerName 
                  AND is_available = TRUE
                FOR UPDATE NOWAIT";
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