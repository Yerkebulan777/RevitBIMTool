using System;
using System.Collections.Generic;

namespace Database.Providers
{
    public static class ProviderFactory
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
            string trimmedName = providerName.Trim();
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
            string trimmedName = name.Trim();
            if (string.IsNullOrEmpty(trimmedName))
            {
                throw new ArgumentException("Provider name cannot be null or empty", nameof(name));
            }
            _providers[trimmedName.ToLowerInvariant()] = factory ?? throw new ArgumentNullException(nameof(factory));
        }



    }
}