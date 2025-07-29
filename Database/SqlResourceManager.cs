using System;

namespace Database
{
    /// <summary>
    /// Упрощенный менеджер SQL ресурсов, который работает с константами вместо embedded files.
    /// 
    /// Эволюция архитектуры:
    /// Раньше: Загрузка SQL из embedded resources → сложность, проблемы совместимости
    /// Теперь: Прямое обращение к константам → простота, универсальность, читаемость
    /// 
    /// Преимущества нового подхода:
    /// 1. Отсутствие зависимости от файловой системы и процесса сборки
    /// 2. Прямая видимость SQL кода в отладчике
    /// 3. Простое тестирование - можно легко подменить запросы для unit тестов
    /// 4. Нет риска "потерять" SQL файлы при развертывании
    /// 
    /// Этот класс служит фасадом (Facade Pattern) для централизованного доступа к SQL запросам.
    /// Даже если сейчас он просто возвращает константы, такая архитектура позволяет легко
    /// добавить дополнительную логику в будущем (логирование, кэширование, валидацию).
    /// </summary>
    public static class SqlResourceManager
    {
        /// <summary>
        /// Получает SQL запрос для создания таблицы принтеров.
        /// 
        /// Этот запрос является фундаментом всей системы - он создает основную таблицу
        /// для хранения состояний принтеров. Выполняется один раз при инициализации
        /// сервиса и использует универсальный SQL синтаксис для максимальной совместимости.
        /// </summary>
        public static string CreatePrinterStatesTable => SqlQueries.CreatePrinterStatesTable;

        /// <summary>
        /// Получает SQL запрос для выборки доступных принтеров с блокировкой.
        /// 
        /// Этот запрос критически важен для предотвращения race conditions.
        /// Когда несколько процессов Revit одновременно пытаются зарезервировать принтер,
        /// FOR UPDATE блокировка гарантирует, что только один из них сможет успешно
        /// выполнить резервирование.
        /// 
        /// Представьте это как систему бронирования мест в кинотеатре - когда один человек
        /// выбирает место, оно временно блокируется для других покупателей, пока первый
        /// не завершит покупку или не отменит бронирование.
        /// </summary>
        public static string GetAvailablePrintersWithLock => SqlQueries.GetAvailablePrintersWithLock;

        /// <summary>
        /// Получает SQL запрос для очистки зависших резервирований.
        /// 
        /// Эта операция решает классическую проблему distributed systems - что делать,
        /// когда процесс, владеющий ресурсом, внезапно завершается (crash, kill -9, BSOD)?
        /// 
        /// Наш подход основан на timeout механизме: если резервирование существует дольше
        /// разумного времени, мы считаем, что владелец "умер" и автоматически освобождаем ресурс.
        /// 
        /// Это похоже на то, как работают парковочные счетчики - если время истекло,
        /// место автоматически становится доступным для других водителей.
        /// </summary>
        public static string CleanupExpiredReservations => SqlQueries.CleanupExpiredReservations;

        /// <summary>
        /// Универсальный метод для получения SQL запроса по логическому имени.
        /// 
        /// Этот метод сохранен для обратной совместимости с существующим кодом,
        /// который мог вызывать GetSqlResource с произвольными строками.
        /// 
        /// В новой архитектуре мы предпочитаем использовать строго типизированные свойства
        /// выше, но этот метод остается как fallback для особых случаев.
        /// </summary>
        /// <param name="resourcePath">Логическое имя SQL ресурса</param>
        /// <returns>SQL запрос как строка</returns>
        /// <exception cref="ArgumentException">Если запрошенный ресурс не найден</exception>
        public static string GetSqlResource(string resourcePath)
        {
            // Простой switch для маппинга старых имен на новые константы
            // Это обеспечивает плавный переход от embedded resources к константам
            return resourcePath switch
            {
                "Tables.CreatePrinterStatesTable" => SqlQueries.CreatePrinterStatesTable,
                "Queries.GetAvailablePrintersWithLock" => SqlQueries.GetAvailablePrintersWithLock,
                "Queries.CleanupExpiredReservations" => SqlQueries.CleanupExpiredReservations,

                // Добавляем поддержку дополнительных запросов для расширяемости
                "Queries.SelectAvailablePrinters" => SqlQueries.SelectAvailablePrinters,
                "Queries.ReadPrinterStateForUpdate" => SqlQueries.ReadPrinterStateForUpdate,
                "DDL.CreatePerformanceIndexes" => SqlQueries.CreatePerformanceIndexes,

                // Для неизвестных ресурсов возвращаем понятную ошибку
                _ => throw new ArgumentException($"SQL ресурс '{resourcePath}' не найден. " +
                    $"Доступные ресурсы: Tables.CreatePrinterStatesTable, Queries.GetAvailablePrintersWithLock, " +
                    $"Queries.CleanupExpiredReservations", nameof(resourcePath))
            };
        }

        /// <summary>
        /// Метод для очистки кэша (заглушка для совместимости).
        /// 
        /// В старой версии с embedded resources этот метод очищал кэш загруженных файлов.
        /// В новой версии кэширование не нужно, поскольку константы уже находятся в памяти,
        /// но мы сохраняем метод для обратной совместимости.
        /// 
        /// Этот паттерн называется "Null Object" - вместо удаления метода мы оставляем
        /// его пустую реализацию, чтобы не сломать существующий код.
        /// </summary>
        public static void ClearCache()
        {
            // В новой архитектуре кэширование не используется, поэтому метод пустой
            // Но его присутствие гарантирует, что старый код не сломается

            // Если в будущем мы добавим логику валидации или трансформации SQL,
            // здесь можно будет сбрасывать соответствующие кэши
        }

        /// <summary>
        /// Вспомогательный метод для получения информации о доступных SQL ресурсах.
        /// Полезен для диагностики и документирования системы.
        /// </summary>
        /// <returns>Массив имен всех доступных SQL ресурсов</returns>
        public static string[] GetAvailableResources()
        {
            return new[]
            {
                "Tables.CreatePrinterStatesTable",
                "Queries.GetAvailablePrintersWithLock",
                "Queries.CleanupExpiredReservations",
                "Queries.SelectAvailablePrinters",
                "Queries.ReadPrinterStateForUpdate",
                "DDL.CreatePerformanceIndexes"
            };
        }
    }
}