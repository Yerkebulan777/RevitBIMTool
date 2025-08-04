using RevitBIMTool.Utils.Common;
using Serilog;
using System;
using System.Collections.Concurrent;
using System.IO;

namespace Database.Logging
{
    public static class LoggerFactory
    {
        private static readonly string documents = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        private static readonly ConcurrentDictionary<Type, ILogger> _loggers = new();

        public static void Initialize(string revitFileName)
        {
            if (Log.Logger != null)
            {
                Log.CloseAndFlush();
            }

            string logDir = Path.Combine(documents, "RevitBIMTool");
            string logPath = Path.Combine(logDir, $"Database-{revitFileName}.txt");

            PathHelper.DeleteExistsFile(logPath);
            PathHelper.EnsureDirectory(logDir);

            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Debug()
                .WriteTo.File(logPath)
                .CreateLogger();
        }

        public static ILogger CreateLogger<T>()
        {
            return _loggers.GetOrAdd(typeof(T), _ => new SerilogAdapter<T>());
        }
    }

    internal class SerilogAdapter<T> : ILogger
    {
        private readonly Serilog.ILogger _logger = Serilog.Log.ForContext<T>();

        public void Debug(string message)
        {
            _logger.Debug(message);
        }

        public void Information(string message)
        {
            _logger.Information(message);
        }

        public void Warning(string message)
        {
            _logger.Warning(message);
        }

        public void Error(string message, Exception exception = null)
        {
            _logger.Error(exception, message);
        }

        public void Log(LogLevel level, string message, string memberName = null)
        {
            switch (level)
            {
                case LogLevel.Debug:
                    _logger.Debug(message);
                    break;
                case LogLevel.Information:
                    _logger.Information(message);
                    break;
                case LogLevel.Warning:
                    _logger.Warning(message);
                    break;
                case LogLevel.Error:
                    _logger.Error(message);
                    break;
            }
        }


    }
}