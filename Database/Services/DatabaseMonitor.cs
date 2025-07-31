using Dapper;
using Database.Logging;
using Database.Stores;
using System;
using System.Data.Odbc;
using System.Text;

namespace Database.Services
{
    public sealed class DatabaseMonitor : IDisposable
    {
        private readonly string _connectionString;
        private readonly ILogger _logger;
        private bool _disposed = false;

        public DatabaseMonitor(string connectionString)
        {
            _connectionString = connectionString;
            LoggerFactory.Initialize(LoggerLevel.Debug);
            _logger = LoggerFactory.CreateLogger<DatabaseMonitor>();
            _logger.Information($"DatabaseMonitor created successfully");
        }

        public string CheckDatabaseHealth()
        {
            StringBuilder report = new StringBuilder();
            bool isHealthy = true;

            _logger.Information("Starting database health check");

            try
            {
                _ = report.AppendLine("=== ПРОВЕРКА СОСТОЯНИЯ БАЗЫ ДАННЫХ ===\n");

                using OdbcConnection connection = CreateConnection();

                bool connectionOk = TestBasicConnection(connection, report);
                isHealthy &= connectionOk;

                if (!connectionOk)
                {
                    _logger.Error("Database connection failed");
                    _ = report.AppendLine("\n❌ СОЕДИНЕНИЕ С БАЗОЙ ДАННЫХ НЕДОСТУПНО");
                    return report.ToString();
                }

                bool schemaOk = ValidateTableStructure(connection, report);
                isHealthy &= schemaOk;

                if (schemaOk)
                {
                    GetDatabaseStatistics(connection, report);
                }

                double responseTime = MeasureResponseTime(connection);
                _ = report.AppendLine($"\n✓ Время отклика БД: {responseTime:F1} мс");

                if (responseTime > 1000)
                {
                    _ = report.AppendLine("⚠️  Медленный отклик базы данных (>1000 мс)");
                    _logger.Warning($"Slow database response: {responseTime:F1} ms");
                }

                if (isHealthy)
                {
                    _ = report.AppendLine("\n🎉 БАЗА ДАННЫХ РАБОТАЕТ КОРРЕКТНО");
                    _logger.Information("Database health check completed successfully");
                }
                else
                {
                    _ = report.AppendLine("\n⚠️  ОБНАРУЖЕНЫ ПРОБЛЕМЫ В РАБОТЕ БД");
                    _logger.Warning("Database health check found issues");
                }

                return report.ToString();
            }
            catch (Exception ex)
            {
                _logger.Error("Critical error during health check", ex);
                _ = report.AppendLine($"\n💥 КРИТИЧЕСКАЯ ОШИБКА");
                _ = report.AppendLine($"Исключение: {ex.Message}");

                if (ex.InnerException != null)
                {
                    _ = report.AppendLine($"Внутренняя ошибка: {ex.InnerException.Message}");
                }

                return report.ToString();
            }
        }

        public bool IsConnectionAvailable()
        {
            try
            {
                using OdbcConnection connection = CreateConnection();
                int testResult = connection.QuerySingle<int>(PrinterSqlStore.TestConnection);
                bool isAvailable = testResult == 1;

                _logger.Debug($"Connection availability check: {isAvailable}");
                return isAvailable;
            }
            catch (Exception ex)
            {
                _logger.Error("Connection availability check failed", ex);
                return false;
            }
        }

        private OdbcConnection CreateConnection()
        {
            _logger.Debug("Creating database connection");
            OdbcConnection connection = new OdbcConnection(_connectionString);
            connection.Open();
            return connection;
        }

        private bool TestBasicConnection(OdbcConnection connection, StringBuilder report)
        {
            try
            {
                _logger.Debug("Testing basic connection");

                int testResult = connection.QuerySingle<int>(PrinterSqlStore.TestConnection);
                string dbVersion = connection.QuerySingleOrDefault<string>(PrinterSqlStore.GetDatabaseVersion);

                _ = report.AppendLine("✓ Соединение с БД установлено успешно");
                _ = report.AppendLine($"✓ Версия БД: {dbVersion ?? "Неизвестно"}");

                try
                {
                    ConnectionInfo connectionInfo = GetConnectionInfo(connection);
                    if (connectionInfo != null)
                    {
                        _ = report.AppendLine($"✓ База данных: {connectionInfo.DatabaseName ?? "N/A"}");
                        _ = report.AppendLine($"✓ Пользователь: {connectionInfo.UserName ?? "N/A"}");
                    }
                }
                catch (Exception ex)
                {
                    _logger.Warning($"Failed to get connection info: {ex.Message}");
                    _ = report.AppendLine("⚠️  Не удалось получить детали подключения");
                }

                _logger.Information("Basic connection test passed");
                return testResult == 1;
            }
            catch (Exception ex)
            {
                _logger.Error("Basic connection test failed", ex);
                _ = report.AppendLine($"✗ Ошибка соединения: {ex.Message}");
                return false;
            }
        }

        private bool ValidateTableStructure(OdbcConnection connection, StringBuilder report)
        {
            try
            {
                _logger.Debug("Validating table structure");

                int tableExists = connection.QuerySingle<int>(PrinterSqlStore.CheckTableExists);

                if (tableExists == 0)
                {
                    _logger.Error("Table printer_states not found");
                    _ = report.AppendLine("✗ Таблица printer_states не найдена");
                    return false;
                }

                int columnCount = connection.QuerySingle<int>(PrinterSqlStore.ValidateTableStructure);

                if (columnCount < 6)
                {
                    _logger.Error($"Incomplete table structure: {columnCount}/6 columns found");
                    _ = report.AppendLine($"✗ Неполная структура таблицы (найдено {columnCount} из 6 столбцов)");
                    return false;
                }

                _logger.Information("Table structure validation passed");
                _ = report.AppendLine("✓ Структура таблицы принтеров корректна");
                return true;
            }
            catch (Exception ex)
            {
                _logger.Error("Table structure validation failed", ex);
                _ = report.AppendLine($"✗ Ошибка валидации схемы: {ex.Message}");
                return false;
            }
        }

        private void GetDatabaseStatistics(OdbcConnection connection, StringBuilder report)
        {
            try
            {
                _logger.Debug("Collecting database statistics");

                PrinterStats stats = GetPrinterStatistics(connection);

                if (stats != null)
                {
                    _ = report.AppendLine("\n=== СТАТИСТИКА ПРИНТЕРОВ ===");
                    _ = report.AppendLine($"✓ Всего принтеров: {stats.TotalPrinters}");
                    _ = report.AppendLine($"✓ Доступно: {stats.AvailablePrinters}");
                    _ = report.AppendLine($"✓ Зарезервировано: {stats.ReservedPrinters}");

                    if (stats.AvgReservationTimeMinutes > 0)
                    {
                        _ = report.AppendLine($"✓ Среднее время резервирования: {stats.AvgReservationTimeMinutes:F1} мин");
                    }

                    _logger.Information("Database statistics collected successfully");
                }
                else
                {
                    _ = report.AppendLine("⚠️  Статистика недоступна");
                }
            }
            catch (Exception ex)
            {
                _logger.Warning($"Failed to collect database statistics: {ex.Message}");
                _ = report.AppendLine($"⚠️  Не удалось получить статистику: {ex.Message}");
            }
        }

        private double MeasureResponseTime(OdbcConnection connection)
        {
            _logger.Debug("Measuring database response time");

            System.Diagnostics.Stopwatch stopwatch = System.Diagnostics.Stopwatch.StartNew();
            _ = connection.QuerySingle<int>(PrinterSqlStore.TestConnection);
            stopwatch.Stop();

            double responseTime = stopwatch.Elapsed.TotalMilliseconds;
            _logger.Debug($"Database response time: {responseTime:F1} ms");

            return responseTime;
        }

        private ConnectionInfo GetConnectionInfo(OdbcConnection connection)
        {
            try
            {
                string dbName = connection.QuerySingleOrDefault<string>(PrinterSqlStore.GetCurrentDatabase);
                string userName = connection.QuerySingleOrDefault<string>(PrinterSqlStore.GetCurrentUser);

                return new ConnectionInfo
                {
                    DatabaseName = dbName,
                    UserName = userName
                };
            }
            catch (Exception ex)
            {
                _logger.Warning($"Failed to get detailed connection info: {ex.Message}");
                return new ConnectionInfo
                {
                    DatabaseName = "Unknown",
                    UserName = "Unknown"
                };
            }
        }

        private PrinterStats GetPrinterStatistics(OdbcConnection connection)
        {
            try
            {
                int totalPrinters = connection.QuerySingle<int>(PrinterSqlStore.GetPrinterStatistics);
                int availablePrinters = connection.QuerySingle<int>(PrinterSqlStore.GetAvailablePrintersCount);
                int reservedPrinters = connection.QuerySingle<int>(PrinterSqlStore.GetReservedPrintersCount);

                double avgTime = 0;

                try
                {
                    double? avgTimeResult = connection.QuerySingleOrDefault<double?>(PrinterSqlStore.GetAverageReservationTime);
                    avgTime = avgTimeResult ?? 0;
                }
                catch (Exception ex)
                {
                    _logger.Debug($"Could not calculate average reservation time: {ex.Message}");
                }

                return new PrinterStats
                {
                    TotalPrinters = totalPrinters,
                    AvailablePrinters = availablePrinters,
                    ReservedPrinters = reservedPrinters,
                    AvgReservationTimeMinutes = avgTime
                };
            }
            catch (Exception ex)
            {
                _logger.Error($"Failed to get printer statistics: {ex.Message}", ex);
                return null;
            }
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _logger.Debug("DatabaseMonitor disposed");
                _disposed = true;
            }
        }

        private sealed class ConnectionInfo
        {
            public string DatabaseName { get; set; }
            public string UserName { get; set; }
        }

        private sealed class PrinterStats
        {
            public int TotalPrinters { get; set; }
            public int AvailablePrinters { get; set; }
            public int ReservedPrinters { get; set; }
            public double AvgReservationTimeMinutes { get; set; }
        }
    }
}