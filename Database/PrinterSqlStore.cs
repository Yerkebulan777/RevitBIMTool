using System;

namespace Database
{
    /// <summary>
    /// Хранилище всех SQL запросов для системы управления принтерами.
    /// </summary>
    public static class PrinterSqlStore
    {
        #region DDL операции - создание структуры базы данных

        /// <summary>
        /// Создает таблицу состояний принтеров с универсальным ODBC-совместимым синтаксисом.
        /// 
        /// Основные отличия от PostgreSQL версии:
        /// - IDENTITY вместо SERIAL для автоинкремента
        /// - DATETIME вместо TIMESTAMPTZ для совместимости
        /// - NEWID() функция для генерации GUID (работает в SQL Server, для других СУБД может потребоваться адаптация)
        /// 
        /// Ограничения целостности применяются на уровне приложения, а не БД,
        /// для максимальной совместимости с различными СУБД.
        /// </summary>
        public const string CreatePrinterStatesTable = @"
            -- Создание основной таблицы для отслеживания состояния принтеров
            -- Используется универсальный SQL синтаксис для максимальной совместимости
            CREATE TABLE printer_states (
                -- Первичный ключ с автоинкрементом (универсальный синтаксис)
                id INTEGER IDENTITY(1,1) PRIMARY KEY,
                
                -- Уникальное имя принтера в системе
                printer_name NVARCHAR(200) NOT NULL UNIQUE,
                
                -- Флаг доступности принтера (1 = доступен, 0 = занят)
                is_available BIT NOT NULL DEFAULT 1,
                
                -- Информация о текущем резервировании
                reserved_by_file NVARCHAR(500) NULL,
                reserved_at DATETIME NULL,
                process_id INTEGER NULL,
                
                -- Токен для оптимистичного блокирования (строка вместо UUID для совместимости)
                change_token NVARCHAR(36) NOT NULL DEFAULT NEWID(),
                
                -- Временные метки для аудита
                created_at DATETIME DEFAULT GETDATE(),
                updated_at DATETIME DEFAULT GETDATE()
            );";

        /// <summary>
        /// Создает индексы для оптимизации производительности запросов.
        /// Эти индексы специально разработаны под паттерны доступа системы управления принтерами.
        /// </summary>
        public const string CreatePerformanceIndexes = @"
            -- Индекс для быстрого поиска доступных принтеров
            -- Этот индекс критически важен для производительности операции резервирования
            CREATE INDEX idx_printer_states_available ON printer_states (is_available)
            WHERE is_available = 1;
            
            -- Индекс для операций очистки зависших резервирований
            -- Ускоряет поиск принтеров, зарезервированных дольше допустимого времени
            CREATE INDEX idx_printer_states_reserved_at ON printer_states (reserved_at)
            WHERE is_available = 0 AND reserved_at IS NOT NULL;
            
            -- Индекс для диагностики и мониторинга процессов
            -- Помогает быстро найти все принтеры, зарезервированные конкретным процессом
            CREATE INDEX idx_printer_states_process_id ON printer_states (process_id)
            WHERE process_id IS NOT NULL;";

        #endregion

        #region DML операции - модификация данных

        /// <summary>
        /// Вstawляет новый принтер в систему с защитой от дублирования.
        /// 
        /// Логика работы:
        /// 1. Пытаемся вставить новый принтер
        /// 2. Если принтер уже существует, команда игнорируется (ON CONFLICT DO NOTHING)
        /// 3. Принтер создается в доступном состоянии по умолчанию
        /// 
        /// Этот подход гарантирует идемпотентность операции инициализации.
        /// </summary>
        public const string InsertPrinter = @"
            -- Безопасная вставка принтера с защитой от дублирования
            -- Использует MERGE для универсальности вместо PostgreSQL-специфичного ON CONFLICT
            MERGE printer_states AS target
            USING (VALUES (@printerName)) AS source (printer_name)
            ON target.printer_name = source.printer_name
            WHEN NOT MATCHED THEN
                INSERT (printer_name, is_available, change_token)
                VALUES (source.printer_name, 1, NEWID());";

        /// <summary>
        /// Освобождает принтер без проверки владельца (административная операция).
        /// 
        /// Используется в следующих сценариях:
        /// - Административное освобождение зависших принтеров
        /// - Очистка состояния при перезапуске системы
        /// - Принудительное освобождение при сбоях
        /// </summary>
        public const string ReleasePrinterSimple = @"
            -- Административное освобождение принтера без проверки прав доступа
            UPDATE printer_states SET
                is_available = 1,
                reserved_by_file = NULL,
                reserved_at = NULL,
                process_id = NULL,
                change_token = NEWID(),
                updated_at = GETDATE()
            WHERE printer_name = @printerName;";

        /// <summary>
        /// Освобождает принтер с проверкой прав доступа.
        /// 
        /// Бизнес-правило: принтер может освободить только тот файл, который его зарезервировал,
        /// либо администратор системы. Это предотвращает случайное освобождение принтера
        /// одним процессом Revit, пока его использует другой.
        /// </summary>
        public const string ReleasePrinterWithPermissionCheck = @"
            -- Освобождение принтера с проверкой прав доступа
            -- Принтер освобождается только если он зарезервирован указанным файлом
            UPDATE printer_states SET
                is_available = 1,
                reserved_by_file = NULL,
                reserved_at = NULL,
                process_id = NULL,
                change_token = NEWID(),
                updated_at = GETDATE()
            WHERE printer_name = @printerName
              AND (reserved_by_file = @revitFileName OR reserved_by_file IS NULL);";

        /// <summary>
        /// Обновляет состояние принтера с оптимистичным блокированием.
        /// 
        /// Механизм оптимистичного блокирования:
        /// 1. Перед обновлением проверяем, что change_token не изменился
        /// 2. Если токен совпадает, обновляем запись и генерируем новый токен
        /// 3. Если токен не совпадает, значит другой процесс изменил запись
        /// 
        /// Этот подход предотвращает lost update проблемы в многопользовательской среде.
        /// </summary>
        public const string UpdatePrinterWithOptimisticLock = @"
            -- Резервирование принтера с оптимистичным блокированием
            -- Обновление происходит только если change_token не был изменен другим процессом
            UPDATE printer_states SET
                is_available = 0,
                reserved_by_file = @revitFileName,
                reserved_at = @reservedAt,
                process_id = @processId,
                change_token = @newToken,
                updated_at = GETDATE()
            WHERE printer_name = @printerName
              AND change_token = @expectedToken;";

        /// <summary>
        /// Очищает зависшие резервирования на основе времени жизни.
        /// 
        /// Алгоритм очистки:
        /// 1. Находим все принтеры, зарезервированные дольше maxAge
        /// 2. Освобождаем их автоматически
        /// 3. Генерируем новые токены для предотвращения конфликтов
        /// 
        /// Эта операция критически важна для предотвращения "мертвых блокировок"
        /// когда процесс Revit завершился аварийно и не освободил ресурсы.
        /// </summary>
        public const string CleanupExpiredReservations = @"
            -- Автоматическая очистка зависших резервирований
            -- Освобождает принтеры, зарезервированные дольше допустимого времени
            UPDATE printer_states 
            SET 
                is_available = 1,
                reserved_by_file = NULL,
                reserved_at = NULL,
                process_id = NULL,
                change_token = NEWID(),
                updated_at = GETDATE()
            WHERE 
                is_available = 0 
                AND reserved_at < @cutoffTime
                AND reserved_at IS NOT NULL;";

        #endregion

        #region Query операции - выборка данных

        /// <summary>
        /// Простая выборка всех доступных принтеров для отображения в интерфейсе.
        /// 
        /// Этот запрос используется для:
        /// - Отображения статуса принтеров пользователю
        /// - Мониторинга состояния системы
        /// - Выбора принтера для ручного резервирования
        /// </summary>
        public const string SelectAvailablePrinters = @"
            -- Получение списка всех доступных принтеров
            -- Сортировка по имени обеспечивает предсказуемый порядок в интерфейсе
            SELECT 
                id, 
                printer_name, 
                is_available, 
                reserved_by_file,
                reserved_at, 
                process_id, 
                change_token
            FROM printer_states
            WHERE is_available = 1
            ORDER BY printer_name;";

        /// <summary>
        /// Читает текущее состояние принтера для оптимистичного блокирования.
        /// 
        /// FOR UPDATE блокировка обеспечивает:
        /// 1. Эксклюзивный доступ к записи до конца транзакции
        /// 2. Предотвращение phantom reads
        /// 3. Консистентность при одновременном доступе
        /// 
        /// Важно: этот запрос должен выполняться внутри транзакции!
        /// </summary>
        public const string ReadPrinterStateForUpdate = @"
            -- Чтение состояния принтера с блокировкой для последующего обновления
            -- FOR UPDATE гарантирует, что между чтением и обновлением никто другой не изменит запись
            SELECT 
                change_token, 
                is_available
            FROM printer_states
            WHERE printer_name = @printerName
            FOR UPDATE;";

        /// <summary>
        /// Получает доступные принтеры с блокировкой для атомарного резервирования.
        /// 
        /// Критический запрос для корректной работы системы резервирования:
        /// 1. Выбираем только доступные принтеры
        /// 2. Блокируем их до конца транзакции
        /// 3. Сортируем по имени для предсказуемого порядка выбора
        /// 
        /// Без FOR UPDATE возможны race conditions, когда несколько процессов
        /// пытаются зарезервировать один и тот же принтер одновременно.
        /// </summary>
        public const string GetAvailablePrintersWithLock = @"
            -- Получение доступных принтеров с блокировкой для предотвращения race conditions
            -- Этот запрос является сердцем системы резервирования принтеров
            SELECT 
                id,
                printer_name,
                is_available,
                reserved_by_file,
                reserved_at,
                process_id,
                change_token
            FROM printer_states
            WHERE is_available = 1
            ORDER BY printer_name
            FOR UPDATE;";

        #endregion

        #region Диагностические запросы

        /// <summary>
        /// Получает полную статистику по состоянию всех принтеров в системе.
        /// Полезен для мониторинга и диагностики проблем.
        /// </summary>
        public const string GetPrinterStatistics = @"
            -- Диагностический запрос для получения полной картины состояния системы
            SELECT 
                COUNT(*) as total_printers,
                SUM(CASE WHEN is_available = 1 THEN 1 ELSE 0 END) as available_printers,
                SUM(CASE WHEN is_available = 0 THEN 1 ELSE 0 END) as reserved_printers,
                AVG(CASE WHEN reserved_at IS NOT NULL 
                    THEN DATEDIFF(minute, reserved_at, GETDATE()) 
                    ELSE NULL END) as avg_reservation_time_minutes
            FROM printer_states;";

        /// <summary>
        /// Находит принтеры с подозрительно долгими резервированиями.
        /// Помогает выявить проблемы до автоматической очистки.
        /// </summary>
        public const string FindSuspiciousReservations = @"
            -- Поиск принтеров с аномально долгими резервированиями
            -- Помогает выявить проблемы в работе системы на ранней стадии
            SELECT 
                printer_name,
                reserved_by_file,
                reserved_at,
                process_id,
                DATEDIFF(minute, reserved_at, GETDATE()) as minutes_reserved
            FROM printer_states
            WHERE is_available = 0 
              AND reserved_at < DATEADD(minute, -@suspiciousThresholdMinutes, GETDATE())
            ORDER BY reserved_at ASC;";

        #endregion
    }
}