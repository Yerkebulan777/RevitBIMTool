using Autodesk.Revit.UI;
using RevitBIMTool.Utils;
using Serilog;
using ServiceLibrary.Helpers;
using ServiceLibrary.Models;
using System.Globalization;
using System.IO;


namespace RevitBIMTool.Core
{
    public sealed class RevitExternalEventHandler : IExternalEventHandler
    {
        private readonly string versionNumber;
        private readonly ExternalEvent externalEvent;
        private readonly string directory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), $"RevitBIMTool");


        public static object SyncLocker { get; } = new();


        public RevitExternalEventHandler(string version)
        {
            externalEvent = ExternalEvent.Create(this);
            versionNumber = version;
        }


        [STAThread]
        public void Execute(UIApplication uiapp)
        {
            AutomationHandler autoHandler = new(uiapp);

            RevitPathHelper.EnsureDirectory(directory);

            Thread.CurrentThread.CurrentCulture = CultureInfo.InvariantCulture;

            while (TaskRequestContainer.Instance.PopTaskModel(versionNumber, out TaskRequest request))
            {
                if (PathHelper.IsFileAccessible(request.RevitFilePath, out string output))
                {
                    Log.Logger = ConfigureLogger(request.RevitFileName);

                    output += autoHandler.RunExecuteTask(request);
                    Log.Information($"Task result:\r\n\t{output}");
                    //MessageManager.SendInfo(request.ChatId, output);

                    Log.CloseAndFlush();
                }

            }

        }


        internal ILogger ConfigureLogger(string logName)
        {
            string logPath = Path.Combine(directory, $"{logName}.txt");

            return new LoggerConfiguration()
                .WriteTo.File(logPath)
                .MinimumLevel.Debug()
                .CreateLogger();
        }


        public string GetName()
        {
            return nameof(RevitExternalEventHandler);
        }


        public ExternalEventRequest Raise()
        {
            return externalEvent.Raise();
        }

    }

}

