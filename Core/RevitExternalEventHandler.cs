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
        private readonly string versionNumber;
        private readonly ExternalEvent externalEvent;
        private readonly string logDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), $"RevitBIMTool");


        public RevitExternalEventHandler(string version)
        {
            externalEvent = ExternalEvent.Create(this);
            spinWait = new SpinWait();
            versionNumber = version;
        }


        public void Execute(UIApplication uiapp)
        {
            StringBuilder builder = new();

            DateTime startTime = DateTime.Now;

            Stopwatch stopwatch = new Stopwatch();

            RevitActionHandler autoHandler = new(uiapp);

            SynchronizationContext context = SynchronizationContext.Current;

            while (TaskRequestContainer.Instance.PopTaskModel(versionNumber, out TaskRequest request))
            {
                spinWait.SpinOnce();

                if (RevitFileHelper.IsTimedOut(startTime))
                {
                    Log.Warning("Timeout reached");
                    return;
                }

                if (GeneralTaskHandler.IsValidTask(ref request))
                {
                    stopwatch.Restart();

                    Log.Logger = ConfigureLogger(request);

                    SynchronizationContext.SetSynchronizationContext(context);

                    string output = autoHandler.RunDocumentAction(uiapp, request, GeneralTaskHandler.RunTask);

                    stopwatch.Stop();

                    string formattedTime = stopwatch.Elapsed.ToString(@"h\:mm\:ss");

                    _ = builder.Append($"{request.RevitFileName}");
                    _ = builder.Append($"[{formattedTime}]");
                    _ = builder.AppendLine(output);

                    Log.Information($"Task result:\r\n\t{builder}");
                }

            }

        }


        internal ILogger ConfigureLogger(TaskRequest request)
        {
            string logName = $"{request.RevitFileName}[{request.CommandNumber}].txt";
            string logPath = Path.Combine(logDirectory, logName);
            RevitPathHelper.EnsureDirectory(logDirectory);
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

