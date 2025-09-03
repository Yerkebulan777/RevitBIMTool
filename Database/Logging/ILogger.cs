using Serilog;

namespace Database.Logging
{
    public interface ILogger
    {
        void LogInformation(string message);
        void LogError(string message, Exception ex = null);
        void LogWarning(string message);
    }

    public class SerilogLogger : ILogger
    {
        private readonly Serilog.ILogger _logger;

        public SerilogLogger()
        {
            _logger = Log.Logger;
        }

        public void LogInformation(string message)
        {
            _logger.Information(message);
        }

        public void LogError(string message, Exception ex = null)
        {
            if (ex != null)
                _logger.Error(ex, message);
            else
                _logger.Error(message);
        }

        public void LogWarning(string message)
        {
            _logger.Warning(message);
        }
    }
}