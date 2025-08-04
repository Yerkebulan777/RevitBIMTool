namespace Database.Stores
{
    /// <summary>
    /// Полное хранилище SQL запросов для PostgreSQL через ODBC
    /// </summary>
    public static class PrinterSqlStore
    {
        #region Schema Management - DDL операции

        /// <summary>
        /// Создает основную таблицу принтеров с оптимизацией для PostgreSQL
        /// </summary>
        public const string CreatePrinterStatesTable = @"
            CREATE TABLE IF NOT EXISTS printer_states (
                id SERIAL PRIMARY KEY,
                printer_name VARCHAR(200) NOT NULL UNIQUE,
                is_available BOOLEAN NOT NULL DEFAULT true,
                reserved_file_name VARCHAR(500),
                reserved_at TIMESTAMPTZ,
                process_id INTEGER,
                version_token UUID NOT NULL DEFAULT gen_random_uuid(),
                job_count INTEGER NOT NULL DEFAULT 0,
                state INTEGER NOT NULL DEFAULT 0,
                created_at TIMESTAMPTZ DEFAULT CURRENT_TIMESTAMP,
                last_update TIMESTAMPTZ DEFAULT CURRENT_TIMESTAMP,
                
                CONSTRAINT chk_printer_name_valid CHECK (LENGTH(TRIM(printer_name)) > 0),
                CONSTRAINT chk_file_name_valid CHECK (reserved_file_name IS NULL OR LENGTH(TRIM(reserved_file_name)) > 0),
                CONSTRAINT chk_state_valid CHECK (state >= 0 AND state <= 4),
                CONSTRAINT chk_job_count_valid CHECK (job_count >= 0),
                CONSTRAINT chk_reservation_logic CHECK (
                    (is_available = true AND reserved_file_name IS NULL AND reserved_at IS NULL AND process_id IS NULL) OR
                    (is_available = false AND reserved_file_name IS NOT NULL AND reserved_at IS NOT NULL AND process_id IS NOT NULL)
                )
            );
            
            CREATE INDEX IF NOT EXISTS idx_printer_states_available 
                ON printer_states (is_available, last_update) 
                WHERE is_available = true;
            
            CREATE INDEX IF NOT EXISTS idx_printer_states_reserved_at 
                ON printer_states (reserved_at) 
                WHERE reserved_at IS NOT NULL;
            
            CREATE INDEX IF NOT EXISTS idx_printer_states_process_id 
                ON printer_states (process_id) 
                WHERE process_id IS NOT NULL;
                
            CREATE TABLE IF NOT EXISTS printer_compensation_log (
                id SERIAL PRIMARY KEY,
                printer_name VARCHAR(200) NOT NULL,
                revit_file VARCHAR(500),
                session_id UUID,
                compensated_at TIMESTAMPTZ DEFAULT CURRENT_TIMESTAMP,
                reason TEXT
            );";

        /// <summary>
        /// Дополнительные ограничения целостности
        /// </summary>
        public static readonly string[] TableConstraints = {
            @"CREATE UNIQUE INDEX IF NOT EXISTS uk_printer_name 
              ON printer_states (LOWER(printer_name));",

            @"ALTER TABLE printer_states 
              ADD CONSTRAINT IF NOT EXISTS chk_printer_name_valid 
              CHECK (LENGTH(TRIM(printer_name)) > 0);",

            @"ALTER TABLE printer_states 
              ADD CONSTRAINT IF NOT EXISTS chk_file_name_valid 
              CHECK (reserved_file_name IS NULL OR LENGTH(TRIM(reserved_file_name)) > 0);",

            @"ALTER TABLE printer_states 
              ADD CONSTRAINT IF NOT EXISTS chk_state_valid 
              CHECK (state >= 0 AND state <= 4);",

            @"ALTER TABLE printer_states 
              ADD CONSTRAINT IF NOT EXISTS chk_job_count_valid 
              CHECK (job_count >= 0);"
        };

        /// <summary>
        /// Проверка существования таблицы
        /// </summary>
        public const string CheckTableExists = @"
            SELECT COUNT(*)::INTEGER 
            FROM information_schema.tables 
            WHERE table_schema = 'public' 
              AND table_name = 'printer_states' 
              AND table_type = 'BASE TABLE';";

        /// <summary>
        /// Валидация структуры таблицы
        /// </summary>
        public const string ValidateTableStructure = @"
            SELECT COUNT(*)::INTEGER 
            FROM information_schema.columns 
            WHERE table_schema = 'public' 
              AND table_name = 'printer_states' 
              AND column_name IN ('id', 'printer_name', 'is_available', 'version_token', 'reserved_at', 'process_id', 'job_count', 'state');";

        /// <summary>
        /// Валидация основных столбцов схемы
        /// </summary>
        public const string ValidateSchemaColumns = @"
            SELECT COUNT(*)::INTEGER 
            FROM information_schema.columns 
            WHERE table_schema = 'public' 
              AND table_name = 'printer_states' 
              AND column_name IN ('id', 'printer_name', 'is_available', 'version_token');";

        #endregion

        #region Connection Testing и System Info

        /// <summary>
        /// Простой тест соединения
        /// </summary>
        public const string TestConnection = "SELECT 1::INTEGER;";

        /// <summary>
        /// Получение версии PostgreSQL
        /// </summary>
        public const string GetDatabaseVersion = "SELECT version();";

        /// <summary>
        /// Получение текущей базы данных
        /// </summary>
        public const string GetCurrentDatabase = "SELECT current_database();";

        /// <summary>
        /// Получение текущего пользователя
        /// </summary>
        public const string GetCurrentUser = "SELECT current_user;";

        #endregion

        #region Core Printer Operations - Оптимизированные запросы

        /// <summary>
        /// Batch-friendly инициализация принтера
        /// </summary>
        public const string InitializePrinter = @"
            INSERT INTO printer_states (printer_name, is_available, version_token, job_count, state, last_update)
            VALUES (@printerName, true, gen_random_uuid(), 0, 0, CURRENT_TIMESTAMP)
            ON CONFLICT (printer_name) DO UPDATE SET
                last_update = CURRENT_TIMESTAMP
            WHERE printer_states.printer_name = EXCLUDED.printer_name;";

        /// <summary>
        /// Получение конкретного принтера с блокировкой
        /// </summary>
        public const string GetSpecificPrinterWithLock = @"
            SELECT id, printer_name as PrinterName, is_available as IsAvailable, 
                   reserved_file_name as RevitFileName, reserved_at, process_id as ProcessId, 
                   version_token as VersionToken, job_count as JobCount, state as State,
                   COALESCE(last_update, created_at) as LastUpdate
            FROM printer_states
            WHERE printer_name = @printerName
            FOR UPDATE NOWAIT;";

        /// <summary>
        /// Атомарное резервирование с оптимистичной блокировкой
        /// </summary>
        public const string ReservePrinter = @"
            UPDATE printer_states SET
                is_available = false,
                reserved_file_name = @revitFileName,
                reserved_at = @reservedAt,
                process_id = @processId,
                version_token = gen_random_uuid(),
                state = 1,
                last_update = CURRENT_TIMESTAMP
            WHERE printer_name = @printerName
              AND version_token = @expectedToken
              AND is_available = true;";

        /// <summary>
        /// Атомарное резервирование принтера
        /// </summary>
        public const string ReservePrinterAtomic = @"
            UPDATE printer_states SET
                is_available = false,
                reserved_file_name = @RevitFileName,
                reserved_at = @ReservedAt,
                process_id = @ProcessId,
                version_token = @SessionId,
                state = @State,
                last_update = CURRENT_TIMESTAMP
            WHERE printer_name = @PrinterName
              AND is_available = true;";

        /// <summary>
        /// Освобождение принтера с проверкой владельца
        /// </summary>
        public const string ReleasePrinter = @"
            UPDATE printer_states SET
                is_available = true,
                reserved_file_name = NULL,
                reserved_at = NULL,
                process_id = NULL,
                version_token = gen_random_uuid(),
                state = 0,
                last_update = CURRENT_TIMESTAMP
            WHERE printer_name = @printerName
              AND (reserved_file_name = @revitFileName OR @revitFileName IS NULL);";

        /// <summary>
        /// Освобождение принтера по сессии
        /// </summary>
        public const string ReleasePrinterBySession = @"
            UPDATE printer_states SET
                is_available = true,
                reserved_file_name = NULL,
                reserved_at = NULL,
                process_id = NULL,
                version_token = gen_random_uuid(),
                state = @finalState,
                last_update = CURRENT_TIMESTAMP
            WHERE printer_name = @printerName
              AND version_token = @sessionId;";

        /// <summary>
        /// Проверка доступности принтера
        /// </summary>
        public const string IsPrinterAvailable = @"
            SELECT is_available 
            FROM printer_states 
            WHERE printer_name = @printerName;";

        /// <summary>
        /// Поиск зависших резервации
        /// </summary>
        public const string FindStuckReservations = @"
            SELECT 
                id,
                printer_name as PrinterName,
                reserved_file_name as RevitFileName,
                reserved_at as ReservedAt,
                process_id as ProcessId,
                version_token as SessionId,
                state as State,
                last_update as LastUpdate,
                EXTRACT(EPOCH FROM (CURRENT_TIMESTAMP - reserved_at))/60 as MinutesStuck
            FROM printer_states
            WHERE is_available = false 
              AND reserved_at < @cutoffTime
              AND reserved_at IS NOT NULL
            ORDER BY reserved_at ASC;";

        /// <summary>
        /// Компенсация зависшей резервации
        /// </summary>
        public const string CompensateStuckReservation = @"
            UPDATE printer_states SET
                is_available = true,
                reserved_file_name = NULL,
                reserved_at = NULL,
                process_id = NULL,
                version_token = gen_random_uuid(),
                state = 4,
                last_update = CURRENT_TIMESTAMP
            WHERE printer_name = @PrinterName
              AND version_token = @SessionId;";

        /// <summary>
        /// Логирование компенсации
        /// </summary>
        public const string LogCompensation = @"
            INSERT INTO printer_compensation_log 
            (printer_name, revit_file, session_id, compensated_at, reason)
            VALUES (@PrinterName, @RevitFileName, @SessionId, CURRENT_TIMESTAMP, @reason)
            ON CONFLICT DO NOTHING;";

        /// <summary>
        /// Улучшенная очистка истекших резервирований
        /// </summary>
        public const string CleanupExpiredReservations = @"
            WITH expired_printers AS (
                UPDATE printer_states 
                SET 
                    is_available = true,
                    reserved_file_name = NULL,
                    reserved_at = NULL,
                    process_id = NULL,
                    version_token = gen_random_uuid(),
                    state = 0,
                    last_update = CURRENT_TIMESTAMP
                WHERE 
                    is_available = false 
                    AND reserved_at < @cutoffTime
                    AND reserved_at IS NOT NULL
                RETURNING printer_name, reserved_file_name, 
                         EXTRACT(EPOCH FROM (CURRENT_TIMESTAMP - reserved_at))/60 as minutes_reserved
            )
            SELECT COUNT(*)::INTEGER as cleaned_count FROM expired_printers;";

        #endregion

        #region Statistics и Monitoring

        /// <summary>
        /// Общая статистика принтеров
        /// </summary>
        public const string GetPrinterStatistics = @"
            SELECT COUNT(*)::INTEGER as total_printers FROM printer_states;";

        /// <summary>
        /// Количество доступных принтеров
        /// </summary>
        public const string GetAvailablePrintersCount = @"
            SELECT COUNT(*)::INTEGER FROM printer_states WHERE is_available = true;";

        /// <summary>
        /// Количество зарезервированных принтеров
        /// </summary>
        public const string GetReservedPrintersCount = @"
            SELECT COUNT(*)::INTEGER FROM printer_states WHERE is_available = false;";

        /// <summary>
        /// Среднее время резервирования в минутах
        /// </summary>
        public const string GetAverageReservationTime = @"
            SELECT COALESCE(AVG(EXTRACT(EPOCH FROM (CURRENT_TIMESTAMP - reserved_at))/60), 0)::DOUBLE PRECISION
            FROM printer_states 
            WHERE reserved_at IS NOT NULL;";

        #endregion

        #region Legacy Support для PrinterRepository

        /// <summary>
        /// Получение активных принтеров (совместимость с PrinterRepository)
        /// </summary>
        public const string GetActivePrinters = @"
            SELECT id, printer_name, state::INTEGER, 
                   COALESCE(last_update, created_at) as last_update,
                   job_count::INTEGER
            FROM printer_states 
            WHERE is_available IN (true, false) 
            ORDER BY COALESCE(last_update, created_at) ASC;";

        /// <summary>
        /// Обновление статуса принтера (совместимость)
        /// </summary>
        public const string UpdatePrinterStatus = @"
            UPDATE printer_states 
            SET last_update = CURRENT_TIMESTAMP 
            WHERE id = @printerId;";

        /// <summary>
        /// Попытка захвата блокировки принтера (legacy)
        /// </summary>
        public const string TryAcquirePrinterLock = @"
            UPDATE printer_states 
            SET is_available = false, 
                reserved_file_name = @lockedBy,
                reserved_at = CURRENT_TIMESTAMP,
                version_token = gen_random_uuid(),
                state = 1,
                last_update = CURRENT_TIMESTAMP
            WHERE id = @printerId AND is_available = true;";

        /// <summary>
        /// Освобождение блокировки принтера (legacy)
        /// </summary>
        public const string ReleasePrinterLock = @"
            UPDATE printer_states 
            SET is_available = true,
                reserved_file_name = NULL,
                reserved_at = NULL,
                version_token = gen_random_uuid(),
                state = 0,
                last_update = CURRENT_TIMESTAMP
            WHERE id = @printerId;";

        #endregion
    }
}