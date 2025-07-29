// Database/SqlResourceManager.cs
using System;
using System.Collections.Concurrent;
using System.IO;
using System.Reflection;

namespace Database
{
    /// <summary>
    /// Менеджер для работы с SQL ресурсами, встроенными в сборку.
    /// 
    /// Этот класс обеспечивает централизованный доступ к SQL файлам и кэширует
    /// их содержимое для повышения производительности. Использование embedded ресурсов
    /// позволяет версионировать SQL код вместе с приложением и получать
    /// intellisense поддержку в SQL файлах.
    /// </summary>
    public static class SqlResourceManager
    {
        // Кэш для загруженных SQL запросов, чтобы не читать файлы повторно
        private static readonly ConcurrentDictionary<string, string> _sqlCache = new ConcurrentDictionary<string, string>();

        // Базовое пространство имен для поиска ресурсов
        private const string BaseNamespace = "Database.SqlResources";

        /// <summary>
        /// Получает SQL запрос для создания таблицы принтеров.
        /// Этот запрос выполняется один раз при инициализации сервиса.
        /// </summary>
        public static string CreatePrinterStatesTable =>
            GetSqlResource("Tables.CreatePrinterStatesTable");

        /// <summary>
        /// Получает SQL запрос для выборки доступных принтеров с блокировкой.
        /// Используется в критических секциях для предотвращения race conditions.
        /// </summary>
        public static string GetAvailablePrintersWithLock =>
            GetSqlResource("Queries.GetAvailablePrintersWithLock");

        /// <summary>
        /// Получает SQL запрос для очистки зависших резервирований.
        /// Помогает автоматически освобождать ресурсы от завершившихся процессов.
        /// </summary>
        public static string CleanupExpiredReservations =>
            GetSqlResource("Queries.CleanupExpiredReservations");

        /// <summary>
        /// Универсальный метод для получения SQL ресурса по относительному пути.
        /// Автоматически кэширует результаты для повышения производительности.
        /// </summary>
        /// <param name="resourcePath">Относительный путь к ресурсу (например, "Tables.CreateUsers")</param>
        /// <returns>Содержимое SQL файла как строка</returns>
        public static string GetSqlResource(string resourcePath)
        {
            // Проверяем кэш перед чтением файла
            return _sqlCache.GetOrAdd(resourcePath, path =>
            {
                string fullResourceName = $"{BaseNamespace}.{path}.sql";

                Assembly assembly = Assembly.GetExecutingAssembly();
                using Stream stream = assembly.GetManifestResourceStream(fullResourceName);

                if (stream == null)
                {
                    throw new InvalidOperationException($"SQL ресурс {fullResourceName} не найден. ");
                }

                using StreamReader reader = new StreamReader(stream);
                string sqlContent = reader.ReadToEnd();

                // Логируем успешную загрузку ресурса для диагностики
                System.Diagnostics.Debug.WriteLine($"Loaded SQL resource: {fullResourceName}");

                return sqlContent;
            });
        }

        /// <summary>
        /// Очищает кэш SQL ресурсов. Полезно для тестирования или
        /// в случае необходимости перезагрузить ресурсы во время выполнения.
        /// </summary>
        public static void ClearCache()
        {
            _sqlCache.Clear();
        }
    }
}