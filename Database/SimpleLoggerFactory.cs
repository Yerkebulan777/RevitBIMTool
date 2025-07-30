using System;

namespace Database
{
    /// <summary>
    /// Фабрика для создания простых логгеров.
    /// </summary>
    public static class SimpleLoggerFactory
    {
        private static LoggerLevel _defaultLevel = LoggerLevel.Information;

        /// <summary>
        /// Устанавливает уровень логирования по умолчанию.
        /// </summary>
        public static void SetDefaultLevel(LoggerLevel level)
        {
            _defaultLevel = level;
        }

        /// <summary>
        /// Создает логгер для указанного типа.
        /// </summary>
        public static ILogger CreateLogger<T>()
        {
            return new Logger(typeof(T).Name, _defaultLevel);
        }

        /// <summary>
        /// Создает логгер с указанным именем категории.
        /// </summary>
        public static ILogger CreateLogger(string categoryName)
        {
            return new Logger(categoryName, _defaultLevel);
        }

        /// <summary>
        /// Создает логгер для указанного типа.
        /// </summary>
        public static ILogger CreateLogger(Type type)
        {
            return new Logger(type.Name, _defaultLevel);
        }

        /// <summary>
        /// Инициализирует систему логирования.
        /// </summary>
        public static void Initialize(LoggerLevel minimumLevel = LoggerLevel.Information, string logDirectory = null)
        {
            _defaultLevel = minimumLevel;
            Logger.Initialize(logDirectory);
        }
    }
}