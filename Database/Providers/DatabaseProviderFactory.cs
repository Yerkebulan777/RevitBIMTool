using Database.Providers;
using System;
using System.Collections.Generic;

namespace Database.Providers
{
    /// <summary>
    /// Фабрика провайдеров баз данных
    /// Это центральная точка, которая определяет, какую базу данных использовать
    /// на основе строки подключения или явного указания типа
    /// 
    /// Паттерн Factory позволяет легко добавлять новые провайдеры
    /// без изменения существующего кода
    /// </summary>
    public static class DatabaseProviderFactory
    {
        /// <summary>
        /// Словарь зарегистрированных провайдеров
        /// Это позволяет легко расширять систему новыми базами данных
        /// </summary>
        private static readonly Dictionary<string, Func<IDatabaseProvider>> _providers =
            new Dictionary<string, Func<IDatabaseProvider>>(StringComparer.OrdinalIgnoreCase)
            {
                { "sqlite", () => new SqliteProvider() },
                { "sqlserver", () => new SqlServerProvider() },
                // Здесь можно легко добавить новые провайдеры:
                // { "postgresql", () => new PostgreSqlProvider() },
                // { "mysql", () => new MySqlProvider() },
            };

        /// <summary>
        /// Создание провайдера по имени
        /// Это основной метод для получения нужного провайдера
        /// </summary>
        public static IDatabaseProvider CreateProvider(string providerName)
        {
            if (string.IsNullOrEmpty(providerName))
                throw new ArgumentException("Provider name cannot be null or empty", nameof(providerName));

            if (_providers.TryGetValue(providerName, out var factory))
            {
                return factory();
            }

            throw new NotSupportedException($"Database provider '{providerName}' is not supported. " +
                $"Available providers: {string.Join(", ", _providers.Keys)}");
        }

        /// <summary>
        /// Автоматическое определение провайдера по строке подключения
        /// Анализирует ключевые слова в строке подключения для определения типа БД
        /// </summary>
        public static IDatabaseProvider CreateProviderFromConnectionString(string connectionString)
        {
            if (string.IsNullOrEmpty(connectionString))
                throw new ArgumentException("Connection string cannot be null or empty", nameof(connectionString));

            var lowerConnectionString = connectionString.ToLowerInvariant();

            // Определяем тип базы данных по ключевым словам в строке подключения
            if (lowerConnectionString.Contains("data source") &&
                (lowerConnectionString.Contains(".db") || lowerConnectionString.Contains(".sqlite")))
            {
                return CreateProvider("sqlite");
            }

            if (lowerConnectionString.Contains("server=") || lowerConnectionString.Contains("data source=") &&
                !lowerConnectionString.Contains(".db"))
            {
                return CreateProvider("sqlserver");
            }

            // По умолчанию используем SQLite как самый универсальный вариант
            return CreateProvider("sqlite");
        }

        /// <summary>
        /// Получение списка доступных провайдеров
        /// Полезно для диагностики и настройки
        /// </summary>
        public static string[] GetAvailableProviders()
        {
            var providers = new string[_providers.Count];
            _providers.Keys.CopyTo(providers, 0);
            return providers;
        }

        /// <summary>
        /// Регистрация нового провайдера
        /// Позволяет добавлять поддержку новых баз данных без изменения кода фабрики
        /// </summary>
        public static void RegisterProvider(string name, Func<IDatabaseProvider> factory)
        {
            if (string.IsNullOrEmpty(name))
                throw new ArgumentException("Provider name cannot be null or empty", nameof(name));

            if (factory == null)
                throw new ArgumentNullException(nameof(factory));

            _providers[name] = factory;
        }
    }
}