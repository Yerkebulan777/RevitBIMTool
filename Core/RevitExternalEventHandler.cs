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

            Thread.CurrentThread.CurrentCulture = CultureInfo.InvariantCulture;

            while (TaskRequestContainer.Instance.PopTaskModel(versionNumber, out TaskRequest taskRequest))
            {
                if (PathHelper.IsFileAccessible(taskRequest.RevitFilePath, out string output))
                {
                    Log.Logger = ConfigureLogger(taskRequest);
                    output = autoHandler.ExecuteTask(taskRequest);
                    Log.Information($"Task result:\r\n\t{output}");
                    Log.CloseAndFlush();
                }

                //MessageManager.SendInfo(taskRequest.ChatId, output);
            }

        }



        internal ILogger ConfigureLogger(TaskRequest taskRequest)
        {
            RevitPathHelper.EnsureDirectory(directory);

            return new LoggerConfiguration()
                .WriteTo.File(Path.Combine(directory, $"{taskRequest.RevitFileName}.txt"),
                    rollingInterval: RollingInterval.Infinite)
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

