// Database/Configuration/DatabaseConfig.cs
using Database.Providers;
using Database.Providers;
using System;
using System.IO;

namespace Database.Configuration
{
    /// <summary>
    /// Конфигурация базы данных с автоматическим определением провайдера
    /// Теперь мы можем работать с любой базой данных, просто меняя строку подключения
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
        /// Инициализация с автоматическим выбором провайдера
        /// Если провайдер не указан явно, определяем его по строке подключения
        /// </summary>
        public void Initialize(string connectionString = null, string providerName = null)
        {
            // Определяем строку подключения
            ConnectionString = GetConnectionString(connectionString);

            // Определяем провайдер базы данных
            if (!string.IsNullOrEmpty(providerName))
            {
                Provider = DatabaseProviderFactory.CreateProvider(providerName);
            }
            else
            {
                Provider = DatabaseProviderFactory.CreateProviderFromConnectionString(ConnectionString);
            }

            // Убеждаемся, что база данных инициализирована
            EnsureDatabaseExists();
        }

        /// <summary>
        /// Получение строки подключения из различных источников
        /// Приоритеты: параметр -> переменная окружения -> файл -> значение по умолчанию
        /// </summary>
        private string GetConnectionString(string connectionString)
        {
            // Приоритет 1: Явно переданная строка
            if (!string.IsNullOrEmpty(connectionString))
                return connectionString;

            // Приоритет 2: Переменная окружения
            var envConnectionString = Environment.GetEnvironmentVariable("DATABASE_CONNECTION_STRING");
            if (!string.IsNullOrEmpty(envConnectionString))
                return envConnectionString;

            // Приоритет 3: Файл конфигурации
            var configConnectionString = LoadConnectionStringFromFile();
            if (!string.IsNullOrEmpty(configConnectionString))
                return configConnectionString;

            // Приоритет 4: SQLite по умолчанию (самый надежный вариант)
            var documentsPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            var dbPath = Path.Combine(documentsPath, "RevitBIMTool", "printers.db");
            var dbDirectory = Path.GetDirectoryName(dbPath);

            if (!Directory.Exists(dbDirectory))
                Directory.CreateDirectory(dbDirectory);

            return $"Data Source={dbPath};Version=3;";
        }

        /// <summary>
        /// Загрузка строки подключения из файла конфигурации
        /// </summary>
        private string LoadConnectionStringFromFile()
        {
            try
            {
                var configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "database.config");

                if (!File.Exists(configPath))
                    return null;

                var lines = File.ReadAllLines(configPath);

                foreach (var line in lines)
                {
                    if (string.IsNullOrWhiteSpace(line) || line.StartsWith("#"))
                        continue;

                    if (line.StartsWith("ConnectionString=", StringComparison.OrdinalIgnoreCase))
                    {
                        return line.Substring("ConnectionString=".Length).Trim();
                    }
                }
            }
            catch (Exception)
            {
                // Если не удалось прочитать файл - не критично
            }

            return null;
        }

        /// <summary>
        /// Создание структуры базы данных при первом запуске
        /// Это гарантирует, что таблицы будут созданы автоматически
        /// </summary>
        private void EnsureDatabaseExists()
        {
            try
            {
                using (var connection = Provider.CreateConnection(ConnectionString))
                {
                    connection.Open();

                    using (var command = connection.CreateCommand())
                    {
                        command.CommandText = Provider.GetCreateTableScript();
                        command.CommandTimeout = CommandTimeout;
                        command.ExecuteNonQuery();
                    }
                }
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(
                    $"Failed to initialize database with provider '{Provider.ProviderName}': {ex.Message}", ex);
            }
        }
    }
}