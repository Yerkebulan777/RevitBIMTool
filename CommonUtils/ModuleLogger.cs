using Serilog;
using Serilog.Context;
using System.Collections.Concurrent;

namespace CommonUtils
{
    public sealed class ModuleLogger : IModuleLogger
    {
        private static readonly ConcurrentDictionary<string, ILogger> _loggerCache = new();

        private readonly ILogger _logger;

        public string LogFilePath { get; }
        public string RevitFileName { get; }
        public string ProjectDirectory { get; }

        private ModuleLogger(ILogger logger, string logFilePath, string revitFileName, string projectDirectory)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            LogFilePath = logFilePath;
            RevitFileName = revitFileName;
            ProjectDirectory = projectDirectory;
        }

        /// <summary>
        /// Создает логгер с явным указанием типа
        /// </summary>
        public static IModuleLogger Create<T>(string revitFilePath)
        {
            if (!File.Exists(revitFilePath))
            {
                throw new FileNotFoundException(revitFilePath);
            }

            string moduleName = ExtractModuleName(typeof(T));
            return CreateInternal(moduleName, revitFilePath);
        }


        private static IModuleLogger CreateInternal(string moduleName, string revitFilePath)
        {
            string projectDirectory = PathHelper.LocateDirectory(revitFilePath, "*PROJECT*");
            string logDirectory = Path.Combine(projectDirectory, "RevitBoost", moduleName);
            string documentName = Path.GetFileNameWithoutExtension(revitFilePath);
            string logFilePath = Path.Combine(logDirectory, $"{documentName}.log");

            string loggerKey = $"{moduleName} ({documentName})";

            // Потокобезопасное получение или создание логгера
            ILogger logger = _loggerCache.GetOrAdd(loggerKey, key =>
            {
                PathHelper.DeleteExistsFile(logFilePath);
                PathHelper.EnsureDirectory(logDirectory);

                TimeSpan interval = TimeSpan.FromSeconds(5);

                return new LoggerConfiguration()
                    .MinimumLevel.Debug()
                    .WriteTo.File(logFilePath, shared: true, flushToDiskInterval: interval)
                    .Enrich.WithProperty("Document", documentName)
                    .Enrich.WithProperty("Module", moduleName)
                    .CreateLogger();
            });

            return new ModuleLogger(logger, logFilePath, documentName, projectDirectory);
        }


        private static string ExtractModuleName(Type type)
        {
            string typeName = type.Name;

            string[] suffixes = { "Command", "Handler", "Service", "Manager" };

            string matchedSuffix = suffixes.FirstOrDefault(typeName.EndsWith);

            return matchedSuffix != null ? typeName[..^matchedSuffix.Length] : typeName;
        }


        public static void ClearCache()
        {
            foreach (ILogger logger in _loggerCache.Values)
            {
                if (logger is IDisposable disposable)
                {
                    disposable.Dispose();
                }
            }
            _loggerCache.Clear();
            Log.CloseAndFlush();
        }


        public void Debug(string message, params object[] args)
        {
            _logger.Debug(message, args);
        }


        public void Information(string message, params object[] args)
        {
            _logger.Information(message, args);
        }


        public void Warning(string message, params object[] args)
        {
            _logger.Warning(message, args);
        }


        public void Error(Exception exception, string message, params object[] args)
        {
            _logger.Error(exception, message, args);
        }


        public void Fatal(Exception exception, string message, params object[] args)
        {
            _logger.Fatal(exception, message, args);
        }


        public IDisposable BeginScope(string module)
        {
            List<IDisposable> disposables =
            [
                LogContext.PushProperty("Module", module),
                LogContext.PushProperty("Document", RevitFileName),
                LogContext.PushProperty("Directory", ProjectDirectory)
            ];

            return new CompositeDisposable(disposables);
        }


        private sealed class CompositeDisposable(List<IDisposable> disposables) : IDisposable
        {
            private readonly List<IDisposable> _disposables = disposables;

            public void Dispose()
            {
                foreach (IDisposable disposable in _disposables)
                {
                    disposable?.Dispose();
                }
            }
        }


    }
}