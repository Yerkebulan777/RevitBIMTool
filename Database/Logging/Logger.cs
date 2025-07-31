using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;

namespace Database
{
    /// <summary>
    /// Уровни логирования.
    /// </summary>
    public enum LoggerLevel
    {
        Debug = 0,
        Information = 1,
        Warning = 2,
        Error = 3
    }

    /// <summary>
    /// Простой интерфейс логгера для проекта Database.
    /// </summary>
    public interface ILogger
    {
        void Debug(string message);
        void Information(string message);
        void Warning(string message);
        void Error(string message, Exception exception = null);
    }

    /// <summary>
    /// Простая реализация логгера для проекта Database.
    /// Пишет в Debug Output и в файл.
    /// </summary>
    public sealed class Logger : ILogger
    {
        private readonly string _categoryName;
        private readonly string _logFilePath;
        private readonly LoggerLevel _minimumLevel;
        private readonly object _lockObject = new object();

        private static string _logDirectory;
        private static bool _isInitialized = false;

        public Logger(string categoryName, LoggerLevel minimumLevel = LoggerLevel.Information)
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
            if (_isInitialized) return;

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
            WriteLog(LoggerLevel.Debug, message);
        }

        public void Information(string message)
        {
            WriteLog(LoggerLevel.Information, message);
        }

        public void Warning(string message)
        {
            WriteLog(LoggerLevel.Warning, message);
        }

        public void Error(string message, Exception exception = null)
        {
            string fullMessage = exception != null ? $"{message} | Exception: {exception}" : message;
            WriteLog(LoggerLevel.Error, fullMessage);
        }

        private void WriteLog(LoggerLevel level, string message,
            [CallerMemberName] string memberName = "",
            [CallerLineNumber] int lineNumber = 0)
        {
            if (level < _minimumLevel) return;

            string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
            string logEntry = $"[{timestamp}] [{level}] {_categoryName}.{memberName}:{lineNumber} - {message}";

            // Вывод в Debug (видно в Visual Studio Output)
            System.Diagnostics.Debug.WriteLine(logEntry);

            // Запись в файл
            WriteToFile(logEntry);
        }

        private void WriteToFile(string logEntry)
        {
            if (string.IsNullOrEmpty(_logFilePath)) return;

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
                    Directory.CreateDirectory(directoryPath);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Failed to create log directory: {ex.Message}");
                }
            }
        }
    }
}