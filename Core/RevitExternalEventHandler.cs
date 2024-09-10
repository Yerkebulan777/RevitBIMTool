using Autodesk.Revit.UI;
using RevitBIMTool.ExportHandlers;
using RevitBIMTool.Utils;
using Serilog;
using ServiceLibrary.Models;
using System.Diagnostics;
using System.IO;
using System.Text;


namespace RevitBIMTool.Core
{
    public sealed class RevitExternalEventHandler : IExternalEventHandler
    {
        private readonly SpinWait spinWait;
        private readonly DateTime startTime;
        private readonly string versionNumber;
        private readonly ExternalEvent externalEvent;
        private readonly string logDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), $"RevitBIMTool");


        public RevitExternalEventHandler(string version)
        {
            externalEvent = ExternalEvent.Create(this);
            spinWait = new SpinWait();
            startTime = DateTime.Now;
            versionNumber = version;
        }


        public void Execute(UIApplication uiapp)
        {
            SynchronizationContext context = SynchronizationContext.Current;
            TaskRequestContainer container = TaskRequestContainer.Instance;

            RevitActionHandler actionHandler = new(uiapp);

            while (!RevitFileHelper.IsTimedOut(startTime))
            {
                if (container.PopTaskModel(versionNumber, out TaskRequest model))
                {
                    if (GeneralTaskHandler.IsValidTask(ref model))
                    {
                        Log.Logger = ConfigureLogger(model);

                        SynchronizationContext.SetSynchronizationContext(context);

                        string output = actionHandler.RunDocumentAction(uiapp, model, GeneralTaskHandler.RunTask);

                         Log.Information($"Task result:\r\n\t{output}");
                    }

                    spinWait.SpinOnce();

                    continue;
                }

                return;

            }

        }


        internal ILogger ConfigureLogger(TaskRequest request)
        {
            string logName = $"{request.RevitFileName}[{request.CommandNumber}].txt";
            string logPath = Path.Combine(logDir, logName);
            RevitPathHelper.EnsureDirectory(logDir);
            RevitPathHelper.DeleteExistsFile(logPath);

            if (Log.Logger != null)
            {
                Log.CloseAndFlush();
            }

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

