using Autodesk.Revit.UI;
using CommunicationService.Models;
using Serilog;
using System.Diagnostics;
using System.IO;


namespace RevitBIMTool.Core
{
    public sealed class RevitExternalEventHandler : IExternalEventHandler
    {
        private readonly string versionNumber;
        private readonly ExternalEvent externalEvent;
        private readonly Process currentProcess = Process.GetCurrentProcess();
        private static readonly string docPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);

        public static readonly object SyncLocker = new();


        public RevitExternalEventHandler(string version)
        {
            externalEvent = ExternalEvent.Create(this);
            versionNumber = version;
        }


        [STAThread]
        public void Execute(UIApplication uiapp)
        {
            currentProcess.PriorityBoostEnabled = true;
            AutomationHandler autoHandler = new(uiapp);

            while (TaskRequestContainer.Instance.PopTaskModel(versionNumber, out TaskRequest taskRequest))
            {
                if (File.Exists(taskRequest.RevitFilePath))
                {
                    string result = autoHandler.ExecuteTask(taskRequest);
                    Log.Information($"Task result:  {result}");

                    Task task = new(async () =>
                    {
                        await Task.Delay(taskRequest.CommandNumber * 1000);
                        await RevitMessageManager.SendInfoAsync(taskRequest.ChatId, result);
                    });

                    task.RunSynchronously();
                }
            }

            CloseRevitApplication(uiapp);
        }


        public ILogger ConfigureLogger()
        {
            int procId = currentProcess.Id;
            string logFileName = $"RevitBIMTool{versionNumber}:{procId}.txt";
            string logFilePath = Path.Combine(docPath, logFileName);

            return new LoggerConfiguration()
                .WriteTo.File(logFilePath)
                .MinimumLevel.Debug()
                .CreateLogger();
        }


        private void CloseRevitApplication(UIApplication uiapp)
        {
            if (!currentProcess.HasExited)
            {
                try
                {
                    Log.Warning("Сlosing the Revit...");
                    uiapp.Application.PurgeReleasedAPIObjects();
                }
                finally
                {
                    currentProcess.Kill();
                    currentProcess?.Dispose();
                }
            }
        }


        public string GetName()
        {
            return nameof(RevitExternalEventHandler);
        }


        public ExternalEventRequest Raise()
        {
            Log.Logger = ConfigureLogger();
            return externalEvent.Raise();
        }


    }

}


