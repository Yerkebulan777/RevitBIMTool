namespace Database.Stores
{
    /// <summary>
    /// Централизованное хранилище всех SQL запросов для системы управления принтеров.
    /// </summary>
    public static class PrinterSqlStore
    {
        #region Schema Management - DDL операции

        /// <summary>
        /// Создает основную таблицу принтеров с ограничениями целостности.
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
                CONSTRAINT chk_state_valid CHECK (state >= 0 AND state <= 3),
                CONSTRAINT chk_job_count_valid CHECK (job_count >= 0),
                CONSTRAINT chk_reservation_logic CHECK (
                    (is_available = true AND reserved_file_name IS NULL AND reserved_at IS NULL AND process_id IS NULL) OR
                    (is_available = false AND reserved_file_name IS NOT NULL AND reserved_at IS NOT NULL AND process_id IS NOT NULL)
                )
            );";

        /// <summary>
        /// Добавляет ограничения целостности данных к таблице принтеров.
        /// </summary>
        public static readonly string[] TableConstraints = {
            "ALTER TABLE printer_states ADD CONSTRAINT uk_printer_name UNIQUE (printer_name);",
            "ALTER TABLE printer_states ADD CONSTRAINT chk_printer_name_valid CHECK (LENGTH(TRIM(printer_name)) > 0);",
            "ALTER TABLE printer_states ADD CONSTRAINT chk_file_name_valid CHECK (reserved_file_name IS NULL OR LENGTH(TRIM(reserved_file_name)) > 0);",
            "ALTER TABLE printer_states ADD CONSTRAINT chk_state_valid CHECK (state >= 0 AND state <= 3);",
            "ALTER TABLE printer_states ADD CONSTRAINT chk_job_count_valid CHECK (job_count >= 0);",
            @"ALTER TABLE printer_states ADD CONSTRAINT chk_reservation_logic CHECK (
                (is_available = true AND reserved_file_name IS NULL AND reserved_at IS NULL AND process_id IS NULL) OR
                (is_available = false AND reserved_file_name IS NOT NULL AND reserved_at IS NOT NULL AND process_id IS NOT NULL)
            );"
        };

        /// <summary>
        /// Проверяет существование таблицы принтеров.
        /// </summary>
        public const string CheckTableExists = @"
            SELECT COUNT(*) 
            FROM information_schema.tables 
            WHERE table_name = 'printer_states' AND table_type = 'BASE TABLE';";

        /// <summary>
        /// Проверяет наличие всех необходимых столбцов.
        /// </summary>
        public const string ValidateTableStructure = @"
            SELECT COUNT(*) 
            FROM information_schema.columns 
            WHERE table_name = 'printer_states' 
            AND column_name IN ('id', 'printer_name', 'is_available', 'version_token', 'reserved_at', 'process_id', 'job_count', 'state');";

        /// <summary>
        /// Валидация схемы - проверка основных столбцов.
        /// </summary>
        public const string ValidateSchemaColumns = @"
            SELECT COUNT(*) 
            FROM information_schema.columns 
            WHERE table_name = 'printer_states' 
            AND column_name IN ('id', 'printer_name', 'is_available', 'version_token');";

        #endregion

        #region Data Operations - DML операции

        /// <summary>
        /// Безопасная инициализация принтера с защитой от дублирования.
        /// </summary>
        public const string InitializePrinter = @"
            INSERT INTO printer_states (printer_name, is_available, version_token, job_count, state, last_update)
            VALUES (@printerName, true, gen_random_uuid(), 0, 0, CURRENT_TIMESTAMP)
            ON CONFLICT (printer_name) DO NOTHING;";

        /// <summary>
        /// Получение доступных принтеров с блокировкой для предотвращения race conditions.
        /// </summary>
        public const string GetAvailablePrintersWithLock = @"
            SELECT id, printer_name as PrinterName, is_available as IsAvailable, 
                   reserved_file_name as RevitFileName, reserved_at, process_id as ProcessId, 
                   version_token as VersionToken, job_count as JobCount, state as State,
                   COALESCE(last_update, created_at) as LastUpdate
            FROM printer_states
            WHERE is_available = true
            ORDER BY printer_name
            FOR UPDATE;";

        /// <summary>
        /// Получение одного доступного принтера с блокировкой для резервирования.
        /// </summary>
        public const string GetSingleAvailablePrinterWithLock = @"
            SELECT id, printer_name as PrinterName, is_available as IsAvailable, 
                   reserved_file_name as RevitFileName, reserved_at, process_id as ProcessId, 
                   version_token as VersionToken, job_count as JobCount, state as State,
                   COALESCE(last_update, created_at) as LastUpdate
            FROM printer_states
            WHERE is_available = true AND printer_name = ANY(@printerNames)
            ORDER BY printer_name
            FOR UPDATE
            LIMIT 1;";

        /// <summary>
        /// Резервирование принтера с оптимистичным блокированием.
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
        /// Освобождение принтера с проверкой прав доступа.
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
        /// Автоматическая очистка зависших резервирований.
        /// </summary>
        public const string CleanupExpiredReservations = @"
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
                AND reserved_at IS NOT NULL;";

        #endregion

        #region Query Operations - Выборка данных

        /// <summary>
        /// Получение всех доступных принтеров.
        /// </summary>
        public const string GetAvailablePrinters = @"
            SELECT id, printer_name as PrinterName, is_available as IsAvailable, 
                   reserved_file_name as RevitFileName, reserved_at, process_id as ProcessId, 
                   version_token as VersionToken, job_count as JobCount, state as State,
                   COALESCE(last_update, created_at) as LastUpdate
            FROM printer_states
            WHERE is_available = true
            ORDER BY printer_name;";

        #endregion

        #region Statistics and Monitoring

        /// <summary>
        /// Получение общей статистики принтеров.
        /// </summary>
        public const string GetPrinterStatistics = @"
            SELECT COUNT(*) as total_printers FROM printer_states;";

        /// <summary>
        /// Получение количества доступных принтеров.
        /// </summary>
        public const string GetAvailablePrintersCount = @"
            SELECT COUNT(*) FROM printer_states WHERE is_available = true;";

        /// <summary>
        /// Получение количества зарезервированных принтеров.
        /// </summary>
        public const string GetReservedPrintersCount = @"
            SELECT COUNT(*) FROM printer_states WHERE is_available = false;";

        /// <summary>
        /// Получение среднего времени резервирования в минутах.
        /// </summary>
        public const string GetAverageReservationTime = @"
            SELECT AVG(EXTRACT(EPOCH FROM (CURRENT_TIMESTAMP - reserved_at))/60) 
            FROM printer_states 
            WHERE reserved_at IS NOT NULL;";

        /// <summary>
        /// Получение информации о подключении к базе данных.
        /// </summary>
        public const string GetCurrentDatabase = "SELECT current_database();";

        /// <summary>
        /// Получение текущего пользователя.
        /// </summary>
        public const string GetCurrentUser = "SELECT current_user;";

        #endregion

        #region Legacy Repository Support

        /// <summary>
        /// Получение активных принтеров для совместимости с PrinterRepository.
        /// </summary>
        public const string GetActivePrinters = @"
            SELECT id, printer_name, state, 
                   COALESCE(last_update, created_at) as last_update,
                   job_count
            FROM printer_states 
            WHERE is_available IN (true, false) 
            ORDER BY COALESCE(last_update, created_at) ASC;";

        /// <summary>
        /// Обновление статуса принтера для совместимости.
        /// </summary>
        public const string UpdatePrinterStatus = @"
            UPDATE printer_states 
            SET last_update = CURRENT_TIMESTAMP 
            WHERE id = @printerId;";

        /// <summary>
        /// Попытка захвата блокировки принтера.
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
        /// Освобождение блокировки принтера.
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

        #region Connection Testing - Проверка соединения

        /// <summary>
        /// Простой запрос для проверки соединения с БД.
        /// </summary>
        public const string TestConnection = "SELECT 1;";

        /// <summary>
        /// Проверка версии PostgreSQL.
        /// </summary>
        public const string GetDatabaseVersion = "SELECT version();";

        #endregion
    }



}