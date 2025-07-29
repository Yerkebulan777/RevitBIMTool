// Database/RevitPrinterService.cs
using Dapper;
using Database.Models;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Odbc;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;

namespace Database
{
    /// <summary>
    /// Сервис управления принтерами, специально разработанный для Revit приложений.
    /// 
    /// Основные принципы проектирования:
    /// - Использует имя файла Revit как естественный идентификатор для резервирования
    /// - Применяет SERIALIZABLE изоляцию для максимальной безопасности в многопроцессорной среде
    /// - Реализует оптимистичное блокирование через change_token для предотвращения lost updates
    /// - Содержит механизм повторных попыток для обработки serialization conflicts
    /// - Автоматически очищает зависшие блокировки от завершившихся процессов
    /// </summary>
    public sealed class RevitPrinterService : IDisposable
    {
        private readonly string _connectionString;
        private readonly int _commandTimeout;
        private readonly int _maxRetryAttempts;
        private readonly int _baseRetryDelayMs;
        private bool _disposed = false;

        /// <summary>
        /// Конструктор сервиса с настройками для надежной работы в условиях конкуренции
        /// </summary>
        /// <param name="connectionString">Строка подключения к PostgreSQL через ODBC</param>
        /// <param name="commandTimeout">Таймаут выполнения SQL команд в секундах</param>
        /// <param name="maxRetryAttempts">Максимальное количество повторных попыток при serialization conflicts</param>
        /// <param name="baseRetryDelayMs">Базовая задержка между попытками в миллисекундах (используется для экспоненциального роста)</param>
        public RevitPrinterService(
            string connectionString,
            int commandTimeout = 30,
            int maxRetryAttempts = 5,
            int baseRetryDelayMs = 50)
        {
            _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
            _commandTimeout = commandTimeout;
            _maxRetryAttempts = maxRetryAttempts;
            _baseRetryDelayMs = baseRetryDelayMs;

            // Сразу при создании сервиса инициализируем структуру базы данных
            InitializeDatabase();
        }

        /// <summary>
        /// Создает таблицу принтеров с комплексными ограничениями целостности данных.
        /// 
        /// Ключевые особенности схемы:
        /// - Использует SERIAL для автоматической генерации ID
        /// - Применяет CHECK constraints для обеспечения логической целостности состояний
        /// - UUID токены для версионирования записей и предотвращения race conditions
        /// - Временные зоны для корректной работы в распределенных системах
        /// </summary>
        private void InitializeDatabase()
        {
            StringBuilder sql = new StringBuilder();

            // Основная структура таблицы с минимально необходимыми полями
            sql.AppendLine("CREATE TABLE IF NOT EXISTS printer_states (");
            sql.AppendLine("    id SERIAL PRIMARY KEY,");
            sql.AppendLine("    printer_name VARCHAR(200) NOT NULL,");
            sql.AppendLine("    is_available BOOLEAN NOT NULL DEFAULT true,");
            sql.AppendLine("    reserved_by_file VARCHAR(500) NULL,");
            sql.AppendLine("    reserved_at TIMESTAMP WITH TIME ZONE NULL,");
            sql.AppendLine("    process_id INTEGER NULL,");
            sql.AppendLine("    change_token UUID NOT NULL DEFAULT gen_random_uuid(),");
            sql.AppendLine();

            // Уникальность имени принтера предотвращает дублирование
            sql.AppendLine("    CONSTRAINT uk_printer_name UNIQUE (printer_name),");
            sql.AppendLine();

            // Критически важное ограничение: логическая целостность состояния резервирования
            // Принтер может быть либо полностью свободен (все поля резервирования NULL)
            // либо полностью зарезервирован (все поля резервирования заполнены)
            sql.AppendLine("    CONSTRAINT chk_reservation_logic CHECK (");
            sql.AppendLine("        (is_available = true AND reserved_by_file IS NULL AND reserved_at IS NULL AND process_id IS NULL) OR");
            sql.AppendLine("        (is_available = false AND reserved_by_file IS NOT NULL AND reserved_at IS NOT NULL AND process_id IS NOT NULL)");
            sql.AppendLine("    ),");
            sql.AppendLine();

            // Дополнительные проверки валидности данных
            sql.AppendLine("    CONSTRAINT chk_printer_name_valid CHECK (LENGTH(TRIM(printer_name)) > 0),");
            sql.AppendLine("    CONSTRAINT chk_file_path_valid CHECK (");
            sql.AppendLine("        reserved_by_file IS NULL OR LENGTH(TRIM(reserved_by_file)) > 0");
            sql.AppendLine("    )");
            sql.AppendLine(");");

            // Выполняем создание таблицы с повторными попытками на случай временных проблем с БД
            ExecuteWithSerializableRetry(connection =>
            {
                return connection.Execute(sql.ToString(), commandTimeout: _commandTimeout);
            });
        }

        /// <summary>
        /// Инициализирует список принтеров в системе управления.
        /// 
        /// Этот метод безопасно добавляет новые принтеры, не затрагивая существующие записи.
        /// Использует ON CONFLICT DO NOTHING для предотвращения ошибок при повторном запуске.
        /// </summary>
        /// <param name="printerNames">Массив имен принтеров для инициализации</param>
        public void InitializePrinters(params string[] printerNames)
        {
            if (printerNames == null || printerNames.Length == 0)
                return;

            // Фильтрация и нормализация входных данных для предотвращения некорректных записей
            var validPrinters = printerNames
                .Where(name => !string.IsNullOrWhiteSpace(name))    // Убираем пустые строки
                .Select(name => name.Trim())                        // Удаляем лишние пробелы
                .Distinct(StringComparer.OrdinalIgnoreCase)         // Исключаем дубликаты
                .Select(name => new { printerName = name });        // Формируем параметры для Dapper

            if (!validPrinters.Any())
                return;

            StringBuilder sql = new StringBuilder();
            sql.AppendLine("INSERT INTO printer_states (printer_name, is_available, change_token)");
            sql.AppendLine("VALUES (@printerName, true, gen_random_uuid())");
            sql.AppendLine("ON CONFLICT (printer_name) DO NOTHING");  // Игнорируем дубликаты

            ExecuteWithSerializableRetry(connection =>
            {
                return connection.Execute(sql.ToString(), validPrinters, commandTimeout: _commandTimeout);
            });
        }

        /// <summary>
        /// Пытается зарезервировать любой доступный принтер для указанного файла Revit.
        /// 
        /// Алгоритм работы:
        /// 1. Получает список всех доступных принтеров с блокировкой строк
        /// 2. Упорядочивает принтеры согласно предпочтениям пользователя
        /// 3. Последовательно пытается зарезервировать каждый принтер
        /// 4. Возвращает имя успешно зарезервированного принтера или null
        /// 
        /// Использует SERIALIZABLE транзакции для гарантии консистентности в многопроцессорной среде.
        /// </summary>
        /// <param name="revitFilePath">Полный путь к файлу Revit</param>
        /// <param name="preferredPrinters">Массив предпочтительных принтеров в порядке приоритета</param>
        /// <returns>Имя зарезервированного принтера или null если все заняты</returns>
        public string TryReserveAnyAvailablePrinter(string revitFilePath, params string[] preferredPrinters)
        {
            if (string.IsNullOrWhiteSpace(revitFilePath))
                throw new ArgumentException("Путь к файлу Revit не может быть пустым", nameof(revitFilePath));

            // Извлекаем только имя файла для более компактного хранения в БД и читаемых логов
            string revitFileName = Path.GetFileName(revitFilePath);

            return ExecuteWithSerializableRetry(connection =>
            {
                // SERIALIZABLE изоляция обеспечивает максимальную защиту от phantom reads и race conditions
                using IDbTransaction transaction = connection.BeginTransaction(IsolationLevel.Serializable);
                try
                {
                    // Получаем все доступные принтеры с явной блокировкой строк
                    // FOR UPDATE предотвращает изменение этих записей другими транзакциями
                    List<PrinterState> availablePrinters = GetAvailablePrintersWithLock(connection, transaction);

                    if (!availablePrinters.Any())
                        return null;

                    // Упорядочиваем принтеры: сначала предпочтительные, затем остальные по алфавиту
                    IEnumerable<PrinterState> orderedPrinters = OrderPrintersByPreference(availablePrinters, preferredPrinters);

                    // Пытаемся зарезервировать первый доступный принтер из упорядоченного списка
                    foreach (PrinterState printer in orderedPrinters)
                    {
                        if (ReservePrinterInternal(connection, transaction, printer.PrinterName, revitFileName))
                        {
                            transaction.Commit();
                            return printer.PrinterName;
                        }
                    }

                    // Если не удалось зарезервировать ни один принтер, откатываем транзакцию
                    transaction.Rollback();
                    return null;
                }
                catch
                {
                    // При любой ошибке откатываем транзакцию для сохранения консистентности
                    transaction.Rollback();
                    throw;
                }
            });
        }

        /// <summary>
        /// Резервирует конкретный принтер для указанного файла Revit.
        /// 
        /// Этот метод полезен когда пользователь хочет использовать определенный принтер,
        /// а не любой доступный. Использует те же принципы безопасности что и TryReserveAnyAvailablePrinter.
        /// </summary>
        /// <param name="printerName">Точное имя принтера для резервирования</param>
        /// <param name="revitFilePath">Полный путь к файлу Revit</param>
        /// <returns>true если принтер успешно зарезервирован, false если недоступен</returns>
        public bool TryReserveSpecificPrinter(string printerName, string revitFilePath)
        {
            if (string.IsNullOrWhiteSpace(printerName))
                throw new ArgumentException("Имя принтера не может быть пустым", nameof(printerName));

            if (string.IsNullOrWhiteSpace(revitFilePath))
                throw new ArgumentException("Путь к файлу Revit не может быть пустым", nameof(revitFilePath));

            string revitFileName = Path.GetFileName(revitFilePath);

            return ExecuteWithSerializableRetry(connection =>
            {
                using IDbTransaction transaction = connection.BeginTransaction(IsolationLevel.Serializable);
                try
                {
                    bool success = ReservePrinterInternal(connection, transaction, printerName, revitFileName);

                    if (success)
                        transaction.Commit();
                    else
                        transaction.Rollback();

                    return success;
                }
                catch
                {
                    transaction.Rollback();
                    throw;
                }
            });
        }

        /// <summary>
        /// Освобождает принтер, зарезервированный для указанного файла Revit.
        /// 
        /// Поддерживает два режима работы:
        /// 1. С проверкой прав доступа (когда указан revitFilePath) - только файл-резерватор может освободить
        /// 2. Административное освобождение (без revitFilePath) - принудительное освобождение любого принтера
        /// </summary>
        /// <param name="printerName">Имя принтера для освобождения</param>
        /// <param name="revitFilePath">Путь к файлу Revit (для проверки прав доступа) или null для админ-режима</param>
        /// <returns>true если принтер успешно освобожден</returns>
        public bool ReleasePrinter(string printerName, string revitFilePath = null)
        {
            if (string.IsNullOrWhiteSpace(printerName))
                throw new ArgumentException("Имя принтера не может быть пустым", nameof(printerName));

            StringBuilder sql = new StringBuilder();
            sql.AppendLine("UPDATE printer_states SET");
            sql.AppendLine("    is_available = true,");
            sql.AppendLine("    reserved_by_file = NULL,");
            sql.AppendLine("    reserved_at = NULL,");
            sql.AppendLine("    process_id = NULL,");
            sql.AppendLine("    change_token = gen_random_uuid()");  // Генерируем новый токен при изменении
            sql.AppendLine("WHERE printer_name = @printerName");

            // Добавляем проверку прав доступа если указан файл
            if (!string.IsNullOrWhiteSpace(revitFilePath))
            {
                string revitFileName = Path.GetFileName(revitFilePath);
                sql.AppendLine("  AND (reserved_by_file = @revitFileName OR reserved_by_file IS NULL)");

                return ExecuteWithSerializableRetry(connection =>
                {
                    int affectedRows = connection.Execute(
                        sql.ToString(),
                        new { printerName = printerName.Trim(), revitFileName },
                        commandTimeout: _commandTimeout);

                    return affectedRows > 0;
                });
            }
            else
            {
                // Административное освобождение без проверки прав
                return ExecuteWithSerializableRetry(connection =>
                {
                    int affectedRows = connection.Execute(
                        sql.ToString(),
                        new { printerName = printerName.Trim() },
                        commandTimeout: _commandTimeout);

                    return affectedRows > 0;
                });
            }
        }

        /// <summary>
        /// Получает список всех доступных принтеров для мониторинга состояния системы.
        /// 
        /// Этот метод предназначен только для чтения и не изменяет состояние системы.
        /// Может использоваться для отображения статистики или диагностики.
        /// </summary>
        /// <returns>Коллекция доступных принтеров с их текущим состоянием</returns>
        public IEnumerable<PrinterState> GetAvailablePrinters()
        {
            StringBuilder sql = new StringBuilder();
            sql.AppendLine("SELECT id, printer_name, is_available, reserved_by_file,");
            sql.AppendLine("       reserved_at, process_id, change_token");
            sql.AppendLine("FROM printer_states");
            sql.AppendLine("WHERE is_available = true");
            sql.AppendLine("ORDER BY printer_name");

            return ExecuteWithSerializableRetry(connection =>
            {
                return connection.Query<PrinterState>(sql.ToString(), commandTimeout: _commandTimeout).ToList();
            });
        }

        /// <summary>
        /// Очищает зависшие резервирования на основе времени и проверки активности процессов.
        /// 
        /// Алгоритм очистки:
        /// 1. Находит все принтеры, зарезервированные дольше указанного времени
        /// 2. Проверяет активность процессов (опционально, можно расширить)
        /// 3. Освобождает принтеры от завершившихся или зависших процессов
        /// 
        /// Этот метод критически важен для предотвращения deadlock ситуаций
        /// когда процессы завершаются аварийно, не освободив ресурсы.
        /// </summary>
        /// <param name="maxAge">Максимальный возраст резервирования перед принудительной очисткой</param>
        /// <returns>Количество очищенных резервирований</returns>
        public int CleanupExpiredReservations(TimeSpan maxAge)
        {
            DateTime cutoffTime = DateTime.UtcNow.Subtract(maxAge);

            StringBuilder sql = new StringBuilder();
            sql.AppendLine("UPDATE printer_states SET");
            sql.AppendLine("    is_available = true,");
            sql.AppendLine("    reserved_by_file = NULL,");
            sql.AppendLine("    reserved_at = NULL,");
            sql.AppendLine("    process_id = NULL,");
            sql.AppendLine("    change_token = gen_random_uuid()");
            sql.AppendLine("WHERE is_available = false");
            sql.AppendLine("  AND reserved_at < @cutoffTime");

            return ExecuteWithSerializableRetry(connection =>
            {
                return connection.Execute(sql.ToString(), new { cutoffTime }, commandTimeout: _commandTimeout);
            });
        }

        #region Внутренние методы для работы с базой данных

        /// <summary>
        /// Создает и открывает соединение с базой данных.
        /// Инкапсулирует детали подключения и обеспечивает единообразное создание соединений.
        /// </summary>
        private OdbcConnection CreateConnection()
        {
            OdbcConnection connection = new OdbcConnection(_connectionString);
            connection.Open();
            return connection;
        }

        /// <summary>
        /// Получает доступные принтеры с явной блокировкой строк для предотвращения race conditions.
        /// 
        /// FOR UPDATE блокирует выбранные строки до конца транзакции, предотвращая:
        /// - Phantom reads (появление новых строк в результате повторного SELECT)
        /// - Non-repeatable reads (изменение данных между SELECT операциями)
        /// - Lost updates (перезапись изменений конкурирующими транзакциями)
        /// </summary>
        private List<PrinterState> GetAvailablePrintersWithLock(IDbConnection connection, IDbTransaction transaction)
        {
            StringBuilder sql = new StringBuilder();
            sql.AppendLine("SELECT id, printer_name, is_available, reserved_by_file,");
            sql.AppendLine("       reserved_at, process_id, change_token");
            sql.AppendLine("FROM printer_states");
            sql.AppendLine("WHERE is_available = true");
            sql.AppendLine("ORDER BY printer_name");
            sql.AppendLine("FOR UPDATE");  // Критически важная блокировка для многопроцессорной среды

            return connection.Query<PrinterState>(
                sql.ToString(),
                transaction: transaction,
                commandTimeout: _commandTimeout).ToList();
        }

        /// <summary>
        /// Внутренняя реализация резервирования принтера с оптимистичным блокированием.
        /// 
        /// Двухэтапный процесс:
        /// 1. Читаем текущий change_token и состояние принтера с блокировкой
        /// 2. Обновляем принтер только если change_token не изменился (оптимистичное блокирование)
        /// 
        /// Это предотвращает lost update проблему когда два процесса одновременно
        /// пытаются зарезервировать один принтер.
        /// </summary>
        private bool ReservePrinterInternal(
            IDbConnection connection,
            IDbTransaction transaction,
            string printerName,
            string revitFileName)
        {
            // Этап 1: Читаем текущее состояние с блокировкой
            StringBuilder selectSql = new StringBuilder();
            selectSql.AppendLine("SELECT change_token, is_available");
            selectSql.AppendLine("FROM printer_states");
            selectSql.AppendLine("WHERE printer_name = @printerName");
            selectSql.AppendLine("FOR UPDATE");

            var currentState = connection.QuerySingleOrDefault<(Guid changeToken, bool isAvailable)>(
                selectSql.ToString(),
                new { printerName = printerName.Trim() },
                transaction,
                _commandTimeout);

            // Проверяем что принтер существует и доступен для резервирования
            if (currentState.changeToken == Guid.Empty || !currentState.isAvailable)
                return false;

            // Этап 2: Обновляем состояние только если токен не изменился
            StringBuilder updateSql = new StringBuilder();
            updateSql.AppendLine("UPDATE printer_states SET");
            updateSql.AppendLine("    is_available = false,");
            updateSql.AppendLine("    reserved_by_file = @revitFileName,");
            updateSql.AppendLine("    reserved_at = @reservedAt,");
            updateSql.AppendLine("    process_id = @processId,");
            updateSql.AppendLine("    change_token = @newToken");
            updateSql.AppendLine("WHERE printer_name = @printerName");
            updateSql.AppendLine("  AND change_token = @expectedToken");  // Оптимистичное блокирование

            Process currentProcess = Process.GetCurrentProcess();
            int affectedRows = connection.Execute(
                updateSql.ToString(),
                new
                {
                    printerName = printerName.Trim(),
                    revitFileName,
                    reservedAt = DateTime.UtcNow,
                    processId = currentProcess.Id,
                    newToken = Guid.NewGuid(),              // Новый токен версии
                    expectedToken = currentState.changeToken // Ожидаемый текущий токен
                },
                transaction,
                _commandTimeout);

            // Если affectedRows = 0, значит токен изменился и резервирование не удалось
            return affectedRows > 0;
        }

        /// <summary>
        /// Упорядочивает принтеры согласно предпочтениям пользователя.
        /// 
        /// Алгоритм сортировки:
        /// 1. Предпочтительные принтеры (из массива preferredPrinters) получают приоритет 0
        /// 2. Остальные принтеры получают приоритет 1
        /// 3. Внутри каждой группы сортировка по алфавиту для предсказуемости
        /// 
        /// Это позволяет пользователям настраивать предпочтения по качеству печати,
        /// скорости работы или другим критериям.
        /// </summary>
        private static IEnumerable<PrinterState> OrderPrintersByPreference(
            IEnumerable<PrinterState> printers,
            string[] preferredPrinters)
        {
            if (preferredPrinters?.Length > 0)
            {
                // Создаем HashSet для быстрого поиска O(1) вместо O(n) поиска в массиве
                HashSet<string> preferredSet = new HashSet<string>(
                    preferredPrinters
                        .Where(p => !string.IsNullOrWhiteSpace(p))
                        .Select(p => p.Trim()),
                    StringComparer.OrdinalIgnoreCase);

                return printers
                    .OrderBy(p => preferredSet.Contains(p.PrinterName) ? 0 : 1)  // Приоритет
                    .ThenBy(p => p.PrinterName);                                 // Алфавитный порядок
            }

            return printers.OrderBy(p => p.PrinterName);
        }

        /// <summary>
        /// Выполняет операцию с базой данных с автоматическими повторными попытками 
        /// при возникновении serialization conflicts.
        /// 
        /// Serialization failures возникают когда PostgreSQL не может гарантировать
        /// SERIALIZABLE изоляцию из-за конкурентного доступа. Стандартная практика -
        /// повторить операцию с экспоненциальной задержкой.
        /// 
        /// Паттерн retry особенно важен в высоконагруженных системах где множество
        /// процессов конкурируют за одни и те же ресурсы.
        /// </summary>
        private T ExecuteWithSerializableRetry<T>(Func<OdbcConnection, T> operation)
        {
            int attempt = 0;

            while (attempt < _maxRetryAttempts)
            {
                try
                {
                    using OdbcConnection connection = CreateConnection();
                    return operation(connection);
                }
                catch (OdbcException ex) when (IsSerializationFailure(ex) && attempt < _maxRetryAttempts - 1)
                {
                    attempt++;

                    // Экспоненциальная задержка с элементом случайности для предотвращения thundering herd
                    int delay = _baseRetryDelayMs * (int)Math.Pow(2, attempt) + new Random().Next(0, 50);
                    Thread.Sleep(delay);
                }
            }

            // Финальная попытка без перехвата serialization исключений
            // Если и она не удалась, исключение будет передано вызывающему коду
            using OdbcConnection connection = CreateConnection();
            return operation(connection);
        }

        /// <summary>
        /// Определяет является ли исключение результатом serialization failure в PostgreSQL.
        /// 
        /// PostgreSQL использует стандартные SQLSTATE коды для классификации ошибок:
        /// - 40001: serialization_failure 
        /// - 40P01: deadlock_detected
        /// 
        /// Эти ошибки указывают на временные проблемы конкуренции, которые можно
        /// решить повторной попыткой операции.
        /// </summary>
        private static bool IsSerializationFailure(OdbcException ex)
        {
            string[] serializationErrorCodes = { "40001", "40P01" };
            return serializationErrorCodes.Any(code => ex.Message.Contains(code));
        }

        #endregion

        #region IDisposable Implementation

        /// <summary>
        /// Освобождает ресурсы сервиса.
        /// В текущей реализации не требует специальной очистки, так как используются
        /// using statements для управления соединениями, но готов для расширения.
        /// </summary>
        public void Dispose()
        {
            if (!_disposed)
            {
                _disposed = true;
            }
        }

        #endregion
    }
}