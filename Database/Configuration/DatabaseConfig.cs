using System;
using System.IO;
using Database.Providers;

namespace Database.Configuration
{
    /// <summary>
    /// Конфигурация, которая может работать с любыми провайдерами
    /// По умолчанию использует InMemoryProvider, но может принимать любые другие
    /// </summary>
    public sealed class DatabaseConfig
    {
        private static readonly Lazy<DatabaseConfig> _instance =
            new Lazy<DatabaseConfig>(() => new DatabaseConfig());

        public static DatabaseConfig Instance => _instance.Value;

        private DatabaseConfig() { }

        public string ConnectionString { get; private set; }
        public int CommandTimeout { get; private set; } = 30;
        public int MaxRetryAttempts { get; private set; } = 3;
        public IDatabaseProvider Provider { get; private set; }

        /// <summary>
        /// Инициализация с возможностью передать провайдер извне
        /// Если провайдер не передан, используется InMemoryProvider
        /// </summary>
        public void Initialize(string connectionString = null, IDatabaseProvider provider = null)
        {
            ConnectionString = GetConnectionString(connectionString);
            Provider = provider ?? new InMemoryProvider();

            // Инициализируем провайдер
            Provider.Initialize(ConnectionString);
        }

        private string GetConnectionString(string connectionString)
        {
            if (!string.IsNullOrEmpty(connectionString))
                return connectionString;

            var envConnectionString = Environment.GetEnvironmentVariable("DATABASE_CONNECTION_STRING");
            if (!string.IsNullOrEmpty(envConnectionString))
                return envConnectionString;

            // Для InMemoryProvider строка подключения не важна
            return "InMemory";
        }
    }
}