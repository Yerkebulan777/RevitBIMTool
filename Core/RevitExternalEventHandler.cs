using Autodesk.Revit.UI;
using RevitBIMTool.Utils;
using Serilog;
using ServiceLibrary.Helpers;
using ServiceLibrary.Models;
using System.IO;


namespace RevitBIMTool.Core
{
    public sealed class RevitExternalEventHandler : IExternalEventHandler
    {
        private readonly DateTime startTime;
        private readonly string versionNumber;
        private readonly ExternalEvent externalEvent;
        private readonly SynchronizationContext context;
        private readonly string logDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), $"RevitBIMTool");


        public RevitExternalEventHandler(string version)
        {
            externalEvent = ExternalEvent.Create(this);
            context = SynchronizationContext.Current;
            startTime = DateTime.Now;
            versionNumber = version;
        }


        [STAThread]
        public void Execute(UIApplication uiapp)
        {
            AutomationHandler autoHandler = new(uiapp);

            while (TaskRequestContainer.Instance.PopTaskModel(versionNumber, out TaskRequest request))
            {
                if (PathHelper.IsFileAccessible(request.RevitFilePath, out string output))
                {
                    Log.Logger = ConfigureLogger(request);

                    output += autoHandler.RunExecuteTask(request, context);
                    Log.Information($"Task result:\r\n\t{output}");

                    if (RevitFileHelper.IsTimedOut(startTime))
                    {
                        break;
                    }

                }
            }

        }


        internal ILogger ConfigureLogger(TaskRequest request)
        {
            Thread.Sleep(1000);

            if (Log.Logger != null)
            {
                Log.CloseAndFlush();
            }

            string logName = $"{request.RevitFileName}[{request.CommandNumber}].txt";
            string logPath = Path.Combine(logDirectory, logName);
            RevitPathHelper.EnsureDirectory(logDirectory);
            RevitPathHelper.DeleteExistsFile(logPath);

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

