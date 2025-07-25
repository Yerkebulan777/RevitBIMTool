using System;
using System.Collections.Generic;

namespace Database.Providers
{
    /// <summary>
    /// Упрощенная фабрика провайдеров для .NET Standard 2.0
    /// Поддерживает только InMemory провайдер из коробки
    /// </summary>
    public static class DatabaseProviderFactory
    {
        private static readonly Dictionary<string, Func<IDatabaseProvider>> _providers =
            new(StringComparer.OrdinalIgnoreCase)
            {
                { "inmemory", () => new InMemoryProvider() },
                { "memory", () => new InMemoryProvider() }
            };

        /// <summary>
        /// Создание провайдера по имени
        /// </summary>
        public static IDatabaseProvider CreateProvider(string providerName)
        {
            if (string.IsNullOrWhiteSpace(providerName))
            {
                throw new ArgumentException("Provider name cannot be null or empty", nameof(providerName));
            }

            if (_providers.TryGetValue(providerName.Trim(), out Func<IDatabaseProvider> factory))
            {
                return factory();
            }

            // По умолчанию используем InMemory для совместимости
            return new InMemoryProvider();
        }

        /// <summary>
        /// Автоматическое определение провайдера по строке подключения
        /// </summary>
        public static IDatabaseProvider CreateProviderFromConnectionString(string connectionString)
        {
            // Для .NET Standard 2.0 всегда возвращаем InMemory
            return new InMemoryProvider();
        }

        /// <summary>
        /// Регистрация нового провайдера (для основного проекта)
        /// </summary>
        public static void RegisterProvider(string name, Func<IDatabaseProvider> factory)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException("Provider name cannot be null or empty", nameof(name));
            }

            _providers[name.Trim().ToLowerInvariant()] = factory ??
                throw new ArgumentNullException(nameof(factory));
        }

        /// <summary>
        /// Получить список доступных провайдеров
        /// </summary>
        public static string[] GetAvailableProviders()
        {
            return new string[] { "inmemory", "memory" };
        }
    }
}