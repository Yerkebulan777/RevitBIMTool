using Serilog;
using System.IO;


namespace RevitBIMTool.Utils.Common
{
    internal static class LoggerHelper
    {
        private static readonly string MyDocuments = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);

        public static void SetupLogger(SynchronizationContext context, string logFileName)
        {
            lock (context)
            {
                if (Log.Logger != null)
                {
                    Log.CloseAndFlush();
                }

                string logDir = Path.Combine(MyDocuments, "RevitBIMTool");
                string logPath = Path.Combine(logDir, $"{logFileName}.txt");
                PathHelper.DeleteExistsFile(logPath);
                PathHelper.EnsureDirectory(logDir);

                Log.Logger = new LoggerConfiguration()
                    .WriteTo.File(logPath)
                    .MinimumLevel.Debug()
                    .CreateLogger();

            }
        }


        public static void SetupLogger(string logFileName)
        {
            if (Log.Logger != null)
            {
                Log.CloseAndFlush();
            }

            string logDir = Path.Combine(MyDocuments, "RevitBIMTool");
            string logPath = Path.Combine(logDir, $"{logFileName}.txt");
            PathHelper.DeleteExistsFile(logPath);
            PathHelper.EnsureDirectory(logDir);

            Log.Logger = new LoggerConfiguration()
                .WriteTo.File(logPath)
                .MinimumLevel.Debug()
                .CreateLogger();
        }

    }

}
