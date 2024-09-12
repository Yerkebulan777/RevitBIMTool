using Serilog;
using ServiceLibrary.Models;
using System.IO;


namespace RevitBIMTool.Utils
{
    internal static class LoggerHelper
    {
        private static readonly string MyDocuments = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);

        public static void SetupLogger(SynchronizationContext context, TaskRequest request)
        {
            lock (context)
            {
                if (Log.Logger != null)
                {
                    Thread.Sleep(100);
                    Log.CloseAndFlush();
                }

                string logDir = Path.Combine(MyDocuments, "RevitBIMTool");
                string logName = $"{request.RevitFileName}[{request.CommandNumber}].txt";
                string logPath = Path.Combine(logDir, logName);
                RevitPathHelper.DeleteExistsFile(logPath);
                RevitPathHelper.EnsureDirectory(logDir);

                Log.Logger = new LoggerConfiguration()
                    .WriteTo.File(logPath)
                    .MinimumLevel.Debug()
                    .CreateLogger();

            }
        }
    }

}
