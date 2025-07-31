namespace Database.Stores
{
    /// <summary>
    /// Централизованное хранилище всех SQL запросов для системы управления принтеров.
    /// Все запросы используют PostgreSQL синтаксис через ODBC без dynamic объектов.
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
                reserved_by_file VARCHAR(500),
                reserved_at TIMESTAMPTZ,
                process_id INTEGER,
                change_token UUID NOT NULL DEFAULT gen_random_uuid(),
                created_at TIMESTAMPTZ DEFAULT CURRENT_TIMESTAMP,
                updated_at TIMESTAMPTZ DEFAULT CURRENT_TIMESTAMP,
                
                CONSTRAINT chk_printer_name_valid CHECK (LENGTH(TRIM(printer_name)) > 0),
                CONSTRAINT chk_file_path_valid CHECK (reserved_by_file IS NULL OR LENGTH(TRIM(reserved_by_file)) > 0),
                CONSTRAINT chk_reservation_logic CHECK (
                    (is_available = true AND reserved_by_file IS NULL AND reserved_at IS NULL AND process_id IS NULL) OR
                    (is_available = false AND reserved_by_file IS NOT NULL AND reserved_at IS NOT NULL AND process_id IS NOT NULL)
                )
            );";

        /// <summary>
        /// Добавляет ограничения целостности данных к таблице принтеров.
        /// </summary>
        public static readonly string[] TableConstraints = {
            "ALTER TABLE printer_states ADD CONSTRAINT uk_printer_name UNIQUE (printer_name);",
            "ALTER TABLE printer_states ADD CONSTRAINT chk_printer_name_valid CHECK (LENGTH(TRIM(printer_name)) > 0);",
            "ALTER TABLE printer_states ADD CONSTRAINT chk_file_path_valid CHECK (reserved_by_file IS NULL OR LENGTH(TRIM(reserved_by_file)) > 0);",
            @"ALTER TABLE printer_states ADD CONSTRAINT chk_reservation_logic CHECK (
                (is_available = true AND reserved_by_file IS NULL AND reserved_at IS NULL AND process_id IS NULL) OR
                (is_available = false AND reserved_by_file IS NOT NULL AND reserved_at IS NOT NULL AND process_id IS NOT NULL)
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
            AND column_name IN ('id', 'printer_name', 'is_available', 'change_token', 'reserved_at', 'process_id');";

        /// <summary>
        /// Валидация схемы - проверка столбцов.
        /// </summary>
        public const string ValidateSchemaColumns = @"
            SELECT COUNT(*) 
            FROM information_schema.columns 
            WHERE table_name = 'printer_states' 
            AND column_name IN ('id', 'printer_name', 'is_available', 'change_token');";

        #endregion

        #region Data Operations - DML операции

        /// <summary>
        /// Безопасная инициализация принтера с защитой от дублирования.
        /// </summary>
        public const string InitializePrinter = @"
            INSERT INTO printer_states (printer_name, is_available, change_token)
            VALUES (@printerName, true, gen_random_uuid())
            ON CONFLICT (printer_name) DO NOTHING;";

        /// <summary>
        /// Получение доступных принтеров с блокировкой для предотвращения race conditions.
        /// </summary>
        public const string GetAvailablePrintersWithLock = @"
            SELECT id, printer_name, is_available, reserved_by_file, reserved_at, process_id, change_token
            FROM printer_states
            WHERE is_available = true
            ORDER BY printer_name
            FOR UPDATE;";

        /// <summary>
        /// Резервирование принтера с оптимистичным блокированием.
        /// </summary>
        public const string ReservePrinter = @"
            UPDATE printer_states SET
                is_available = false,
                reserved_by_file = @revitFileName,
                reserved_at = @reservedAt,
                process_id = @processId,
                change_token = gen_random_uuid(),
                updated_at = CURRENT_TIMESTAMP
            WHERE printer_name = @printerName
              AND change_token = @expectedToken
              AND is_available = true;";

        /// <summary>
        /// Освобождение принтера с проверкой прав доступа.
        /// </summary>
        public const string ReleasePrinter = @"
            UPDATE printer_states SET
                is_available = true,
                reserved_by_file = NULL,
                reserved_at = NULL,
                process_id = NULL,
                change_token = gen_random_uuid(),
                updated_at = CURRENT_TIMESTAMP
            WHERE printer_name = @printerName
              AND (reserved_by_file = @revitFileName OR @revitFileName IS NULL);";

        /// <summary>
        /// Автоматическая очистка зависших резервирований.
        /// </summary>
        public const string CleanupExpiredReservations = @"
            UPDATE printer_states 
            SET 
                is_available = true,
                reserved_by_file = NULL,
                reserved_at = NULL,
                process_id = NULL,
                change_token = gen_random_uuid(),
                updated_at = CURRENT_TIMESTAMP
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
            SELECT id, printer_name, is_available, reserved_by_file, reserved_at, process_id, change_token
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
            SELECT id, printer_name as name, 
                   CASE WHEN is_available = true THEN 0 ELSE 1 END as state,
                   COALESCE(updated_at, created_at) as last_update,
                   0 as job_count
            FROM printer_states 
            WHERE is_available IN (true, false) 
            ORDER BY COALESCE(updated_at, created_at) ASC;";

        /// <summary>
        /// Обновление статуса принтера для совместимости.
        /// </summary>
        public const string UpdatePrinterStatus = @"
            UPDATE printer_states 
            SET updated_at = CURRENT_TIMESTAMP 
            WHERE id = @printerId;";

        /// <summary>
        /// Попытка захвата блокировки принтера.
        /// </summary>
        public const string TryAcquirePrinterLock = @"
            UPDATE printer_states 
            SET is_available = false, 
                reserved_by_file = @lockedBy,
                reserved_at = CURRENT_TIMESTAMP,
                change_token = gen_random_uuid()
            WHERE id = @printerId AND is_available = true;";

        /// <summary>
        /// Освобождение блокировки принтера.
        /// </summary>
        public const string ReleasePrinterLock = @"
            UPDATE printer_states 
            SET is_available = true,
                reserved_by_file = NULL,
                reserved_at = NULL,
                change_token = gen_random_uuid()
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