// Database/Stores/PrinterSqlStore.cs
namespace Database.Stores
{
    /// <summary>
    /// Централизованное хранилище всех SQL запросов для системы управления принтеров.
    /// Все запросы используют PostgreSQL синтаксис через ODBC с правильным маппингом на модель PrinterInfo.
    /// </summary>
    public static class PrinterSqlStore
    {
        #region Schema Management - DDL операции

        /// <summary>
        /// Создает основную таблицу принтеров с полной поддержкой модели PrinterInfo.
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
                job_count INTEGER NOT NULL DEFAULT 0,
                state INTEGER NOT NULL DEFAULT 0,
                created_at TIMESTAMPTZ DEFAULT CURRENT_TIMESTAMP,
                updated_at TIMESTAMPTZ DEFAULT CURRENT_TIMESTAMP,
                
                CONSTRAINT chk_printer_name_valid CHECK (LENGTH(TRIM(printer_name)) > 0),
                CONSTRAINT chk_file_path_valid CHECK (reserved_by_file IS NULL OR LENGTH(TRIM(reserved_by_file)) > 0),
                CONSTRAINT chk_state_valid CHECK (state >= 0 AND state <= 3),
                CONSTRAINT chk_job_count_valid CHECK (job_count >= 0),
                CONSTRAINT chk_reservation_logic CHECK (
                    (is_available = true AND reserved_by_file IS NULL AND reserved_at IS NULL AND process_id IS NULL) OR
                    (is_available = false AND reserved_by_file IS NOT NULL AND reserved_at IS NOT NULL AND process_id IS NOT NULL)
                )
            );";

        /// <summary>
        /// Проверяет существование таблицы принтеров.
        /// </summary>
        public const string CheckTableExists = @"
            SELECT COUNT(*) 
            FROM information_schema.tables 
            WHERE table_name = 'printer_states' AND table_type = 'BASE TABLE';";

        /// <summary>
        /// Проверяет наличие всех необходимых столбцов для модели PrinterInfo.
        /// </summary>
        public const string ValidateTableStructure = @"
            SELECT COUNT(*) 
            FROM information_schema.columns 
            WHERE table_name = 'printer_states' 
            AND column_name IN ('id', 'printer_name', 'is_available', 'change_token', 'reserved_at', 'process_id', 'job_count', 'state');";

        /// <summary>
        /// Миграция для добавления недостающих полей job_count и state.
        /// </summary>
        public const string MigratePrinterInfoFields = @"
            DO $$
            BEGIN
                IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'printer_states' AND column_name = 'job_count') THEN
                    ALTER TABLE printer_states ADD COLUMN job_count INTEGER NOT NULL DEFAULT 0;
                END IF;
                
                IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'printer_states' AND column_name = 'state') THEN
                    ALTER TABLE printer_states ADD COLUMN state INTEGER NOT NULL DEFAULT 0;
                END IF;
                
                IF NOT EXISTS (SELECT 1 FROM information_schema.table_constraints WHERE constraint_name = 'chk_state_valid') THEN
                    ALTER TABLE printer_states ADD CONSTRAINT chk_state_valid CHECK (state >= 0 AND state <= 3);
                END IF;
                
                IF NOT EXISTS (SELECT 1 FROM information_schema.table_constraints WHERE constraint_name = 'chk_job_count_valid') THEN
                    ALTER TABLE printer_states ADD CONSTRAINT chk_job_count_valid CHECK (job_count >= 0);
                END IF;
            END $$;";

        #endregion

        #region Data Operations - DML операции

        /// <summary>
        /// Безопасная инициализация принтера с поддержкой всех полей модели PrinterInfo.
        /// </summary>
        public const string InitializePrinter = @"
            INSERT INTO printer_states (printer_name, is_available, change_token, job_count, state, updated_at)
            VALUES (@printerName, true, gen_random_uuid(), 0, 0, CURRENT_TIMESTAMP)
            ON CONFLICT (printer_name) DO UPDATE SET
                updated_at = CURRENT_TIMESTAMP;";

        /// <summary>
        /// Получение доступных принтеров с блокировкой и правильным маппингом на PrinterInfo.
        /// </summary>
        public const string GetAvailablePrintersWithLock = @"
            SELECT 
                id as Id,
                printer_name as PrinterName,
                is_available as IsAvailable,
                reserved_by_file as ReservedFileName,
                updated_at as LastUpdate,
                process_id as ProcessId,
                change_token as VersionToken,
                job_count as JobCount,
                state as State
            FROM printer_states
            WHERE is_available = true
            ORDER BY printer_name
            FOR UPDATE;";

        /// <summary>
        /// Получение информации о конкретном принтере с блокировкой.
        /// </summary>
        public const string GetPrinterInfoWithLock = @"
            SELECT 
                id as Id,
                printer_name as PrinterName,
                is_available as IsAvailable,
                reserved_by_file as ReservedFileName,
                updated_at as LastUpdate,
                process_id as ProcessId,
                change_token as VersionToken,
                job_count as JobCount,
                state as State
            FROM printer_states
            WHERE printer_name = @printerName
            FOR UPDATE;";

        /// <summary>
        /// Резервирование принтера с оптимистичным блокированием и обновлением статуса.
        /// </summary>
        public const string ReservePrinter = @"
            UPDATE printer_states SET
                is_available = false,
                reserved_by_file = @revitFileName,
                reserved_at = @reservedAt,
                process_id = @processId,
                change_token = gen_random_uuid(),
                state = CASE WHEN state = 0 THEN 1 ELSE state END,
                updated_at = CURRENT_TIMESTAMP
            WHERE printer_name = @printerName
              AND change_token = @expectedToken
              AND is_available = true;";

        /// <summary>
        /// Освобождение принтера с проверкой прав доступа и сбросом статуса.
        /// </summary>
        public const string ReleasePrinter = @"
            UPDATE printer_states SET
                is_available = true,
                reserved_by_file = NULL,
                reserved_at = NULL,
                process_id = NULL,
                state = 0,
                change_token = gen_random_uuid(),
                updated_at = CURRENT_TIMESTAMP
            WHERE printer_name = @printerName
              AND (reserved_by_file = @revitFileName OR @revitFileName IS NULL);";

        /// <summary>
        /// Автоматическая очистка зависших резервирований с правильной логикой времени.
        /// </summary>
        public const string CleanupExpiredReservations = @"
            UPDATE printer_states 
            SET 
                is_available = true,
                reserved_by_file = NULL,
                reserved_at = NULL,
                process_id = NULL,
                state = 0,
                change_token = gen_random_uuid(),
                updated_at = CURRENT_TIMESTAMP
            WHERE 
                is_available = false 
                AND reserved_at IS NOT NULL
                AND reserved_at < @cutoffTime;";

        #endregion

        #region Query Operations - Выборка данных

        /// <summary>
        /// Получение всех доступных принтеров с полным маппингом на PrinterInfo.
        /// </summary>
        public const string GetAvailablePrinters = @"
            SELECT 
                id as Id,
                printer_name as PrinterName,
                is_available as IsAvailable,
                reserved_by_file as ReservedFileName,
                updated_at as LastUpdate,
                process_id as ProcessId,
                change_token as VersionToken,
                job_count as JobCount,
                state as State
            FROM printer_states
            WHERE is_available = true
            ORDER BY printer_name;";

        /// <summary>
        /// Получение всех принтеров с полной информацией.
        /// </summary>
        public const string GetAllPrinters = @"
            SELECT 
                id as Id,
                printer_name as PrinterName,
                is_available as IsAvailable,
                reserved_by_file as ReservedFileName,
                updated_at as LastUpdate,
                process_id as ProcessId,
                change_token as VersionToken,
                job_count as JobCount,
                state as State
            FROM printer_states
            ORDER BY printer_name;";

        /// <summary>
        /// Получение принтеров по списку имен с фильтрацией.
        /// </summary>
        public const string GetPrintersByNames = @"
            SELECT 
                id as Id,
                printer_name as PrinterName,
                is_available as IsAvailable,
                reserved_by_file as ReservedFileName,
                updated_at as LastUpdate,
                process_id as ProcessId,
                change_token as VersionToken,
                job_count as JobCount,
                state as State
            FROM printer_states
            WHERE printer_name = ANY(@printerNames)
            ORDER BY printer_name;";

        #endregion

        #region Update Operations - Обновление статуса

        /// <summary>
        /// Обновление количества заданий печати для принтера.
        /// </summary>
        public const string UpdatePrinterJobCount = @"
            UPDATE printer_states 
            SET 
                job_count = @jobCount,
                updated_at = CURRENT_TIMESTAMP
            WHERE printer_name = @printerName;";

        /// <summary>
        /// Обновление состояния принтера.
        /// </summary>
        public const string UpdatePrinterState = @"
            UPDATE printer_states 
            SET 
                state = @state,
                updated_at = CURRENT_TIMESTAMP
            WHERE printer_name = @printerName;";

        /// <summary>
        /// Комплексное обновление статуса принтера.
        /// </summary>
        public const string UpdatePrinterStatus = @"
            UPDATE printer_states 
            SET 
                state = @state,
                job_count = @jobCount,
                updated_at = CURRENT_TIMESTAMP
            WHERE printer_name = @printerName;";

        #endregion

        #region Statistics and Monitoring - Статистика и мониторинг

        /// <summary>
        /// Получение статистики принтеров для мониторинга.
        /// </summary>
        public const string GetPrinterStatistics = @"
            SELECT 
                COUNT(*) as TotalPrinters,
                SUM(CASE WHEN is_available = true THEN 1 ELSE 0 END) as AvailablePrinters,
                SUM(CASE WHEN is_available = false THEN 1 ELSE 0 END) as ReservedPrinters,
                AVG(CASE 
                    WHEN reserved_at IS NOT NULL 
                    THEN EXTRACT(EPOCH FROM (CURRENT_TIMESTAMP - reserved_at))/60 
                    ELSE NULL 
                END) as AvgReservationTimeMinutes
            FROM printer_states;";

        /// <summary>
        /// Поиск зависших принтеров по времени резервирования.
        /// </summary>
        public const string FindStuckPrinters = @"
            SELECT 
                id as Id,
                printer_name as PrinterName,
                is_available as IsAvailable,
                reserved_by_file as ReservedFileName,
                updated_at as LastUpdate,
                process_id as ProcessId,
                change_token as VersionToken,
                job_count as JobCount,
                state as State
            FROM printer_states
            WHERE is_available = false 
              AND reserved_at IS NOT NULL
              AND reserved_at < @stuckThreshold;";

        /// <summary>
        /// Получение принтеров по состоянию.
        /// </summary>
        public const string GetPrintersByState = @"
            SELECT 
                id as Id,
                printer_name as PrinterName,
                is_available as IsAvailable,
                reserved_by_file as ReservedFileName,
                updated_at as LastUpdate,
                process_id as ProcessId,
                change_token as VersionToken,
                job_count as JobCount,
                state as State
            FROM printer_states
            WHERE state = @state
            ORDER BY printer_name;";

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

        /// <summary>
        /// Получение информации о текущем подключении.
        /// </summary>
        public const string GetConnectionInfo = @"
            SELECT 
                current_database() as DatabaseName,
                current_user as UserName,
                inet_server_addr() as ServerAddress,
                inet_server_port() as ServerPort;";

        #endregion

        #region Maintenance Operations - Операции обслуживания

        /// <summary>
        /// Сброс всех резервирований (для обслуживания).
        /// </summary>
        public const string ResetAllReservations = @"
            UPDATE printer_states 
            SET 
                is_available = true,
                reserved_by_file = NULL,
                reserved_at = NULL,
                process_id = NULL,
                state = 0,
                change_token = gen_random_uuid(),
                updated_at = CURRENT_TIMESTAMP
            WHERE is_available = false;";

        /// <summary>
        /// Удаление принтера из системы.
        /// </summary>
        public const string DeletePrinter = @"
            DELETE FROM printer_states 
            WHERE printer_name = @printerName;";

        /// <summary>
        /// Принудительное освобождение зависшего принтера.
        /// </summary>
        public const string ForceReleasePrinter = @"
            UPDATE printer_states 
            SET 
                is_available = true,
                reserved_by_file = NULL,
                reserved_at = NULL,
                process_id = NULL,
                state = 0,
                change_token = gen_random_uuid(),
                updated_at = CURRENT_TIMESTAMP
            WHERE printer_name = @printerName;";

        #endregion



    }
}