using System;

namespace Database
{
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
    /// Уровни логирования.
    /// </summary>
    public enum LoggerLevel
    {
        Debug = 0,
        Information = 1,
        Warning = 2,
        Error = 3
    }
}