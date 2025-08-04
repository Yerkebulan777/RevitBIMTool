namespace CommonUtils
{
    public interface IModuleLogger
    {
        string LogFilePath { get; set; }
        void Debug(string message, params object[] args);
        void Information(string message, params object[] args);
        void Warning(string message, params object[] args);
        void Error(Exception exception, string message, params object[] args);
        void Fatal(Exception exception, string message, params object[] args);
        IDisposable BeginScope(string name, params (string key, object value)[] properties);
    }
}