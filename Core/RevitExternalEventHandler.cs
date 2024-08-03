using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Events;
using CommunicationService.Models;
using Serilog;
using System.Diagnostics;
using System.IO;


namespace RevitBIMTool.Core
{
    public sealed class RevitExternalEventHandler : IExternalEventHandler
    {
        private static int counter = 0;
        private readonly string versionNumber;
        private readonly ExternalEvent externalEvent;
        private static readonly Process currentProcess = Process.GetCurrentProcess();
        private static readonly string docPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        private static readonly string logFileName = $"RevitBIMTool:{currentProcess.Id}.txt";

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

            uiapp.Idling += new EventHandler<IdlingEventArgs>(OnIdlingAsync);

            while (TaskRequestContainer.Instance.PopTaskModel(versionNumber, out TaskRequest taskRequest))
            {
                if (File.Exists(taskRequest.RevitFilePath))
                {
                    string result = autoHandler.ExecuteTask(taskRequest);

                    Log.Information($"Task result:\r\n\t{result}");

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
            return new LoggerConfiguration()
                .WriteTo.File(Path.Combine(docPath, logFileName))
                .MinimumLevel.Debug()
                .CreateLogger();
        }


        private async void OnIdlingAsync(object sender, IdlingEventArgs e)
        {
            Log.Debug($"Idling session called {counter}");

            while (true)
            {
                counter++;

                await Task.Delay(1000);

                if (counter > 1000)
                {
                    CloseRevitApplication();
                }

            }
        }


        private void CloseRevitApplication(UIApplication uiapp = null)
        {
            try
            {
                Log.Warning("Сlose Revit process...");
                uiapp?.Application.PurgeReleasedAPIObjects();
            }
            finally
            {
                Log.CloseAndFlush();
                currentProcess?.Kill();
                currentProcess?.Dispose();
            }
        }


        public string GetName()
        {
            return nameof(RevitExternalEventHandler);
        }


        public ExternalEventRequest Raise()
        {
            Log.Information($"Run {logFileName}");
            Log.Logger = ConfigureLogger();
            return externalEvent.Raise();
        }

    }

}


