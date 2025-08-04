using System;
using System.IO;
using System.Runtime.CompilerServices;

namespace Database.Logging
{
    public interface ILogger
    {
        void Debug(string message);
        void Information(string message);
        void Warning(string message);
        void Error(string message, Exception exception = null);
        void Log(LogLevel level, string message, string memberName = null);
    }

    public enum LogLevel
    {
        Debug,
        Information,
        Warning,
        Error
    }


    public sealed class Logger : ILogger
    {
        private readonly string _categoryName;
        private readonly string _logFilePath;
        private readonly LogLevel _minimumLevel;
        private readonly object _lockObject = new();

        private static string _logDirectory;
        private static bool _isInitialized = false;

        public Logger(string categoryName, LogLevel minimumLevel = LogLevel.Information)
        {
            _categoryName = categoryName ?? throw new ArgumentNullException(nameof(categoryName));
            _minimumLevel = minimumLevel;

            EnsureInitialized();
            _logFilePath = Path.Combine(_logDirectory, $"Database_{DateTime.Now:yyyy-MM-dd}.log");
        }

        /// <summary>
        /// Инициализирует директорию для логов.
        /// </summary>
        public static void Initialize(string logDirectory = null)
        {
            if (_isInitialized)
            {
                return;
            }

            if (string.IsNullOrEmpty(logDirectory))
            {
                logDirectory = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                    "RevitBIMTool", "Database");
            }

            EnsureDirectoryExists(logDirectory);
            _logDirectory = logDirectory;
            _isInitialized = true;
        }

        public void Debug(string message)
        {
            Log(LogLevel.Debug, message);
        }

        public void Information(string message)
        {
            Log(LogLevel.Information, message);
        }

        public void Warning(string message)
        {
            Log(LogLevel.Warning, message);
        }

        public void Error(string message, Exception exception = null)
        {
            string fullMessage = exception != null ? $"{message} | Exception: {exception}" : message;
            Log(LogLevel.Error, fullMessage);
        }

        public void Log(LogLevel level, string message, [CallerMemberName] string memberName = "")
        {
            if (level >= _minimumLevel)
            {
                string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
                string logEntry = $"[{timestamp}] [{level}] {_categoryName}.{memberName}: {message}";

                // Вывод в Debug (видно в Visual Studio Output)
                System.Diagnostics.Debug.WriteLine(logEntry);

                // Запись в файл
                WriteToFile(logEntry);
            }
        }

        public void LogOperationResult(string operation, bool success, TimeSpan elapsed)
        {
            string status = success ? "SUCCESS" : "FAILED";
            Information($"{operation} => {status} in {elapsed.TotalMilliseconds:F0}ms");
        }

        private void WriteToFile(string logEntry)
        {
            if (string.IsNullOrEmpty(_logFilePath))
            {
                return;
            }

            try
            {
                lock (_lockObject)
                {
                    File.AppendAllText(_logFilePath, logEntry + Environment.NewLine);
                }
            }
            catch (Exception ex)
            {
                // Fallback только в Debug если не можем писать в файл
                System.Diagnostics.Debug.WriteLine($"Failed to write to log file: {ex.Message}");
            }
        }

        private static void EnsureInitialized()
        {
            if (!_isInitialized)
            {
                Initialize();
            }
        }

        private static void EnsureDirectoryExists(string directoryPath)
        {
            if (!Directory.Exists(directoryPath))
            {
                try
                {
                    _ = Directory.CreateDirectory(directoryPath);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Failed to create log directory: {ex.Message}");
                }
            }
        }

    }
}