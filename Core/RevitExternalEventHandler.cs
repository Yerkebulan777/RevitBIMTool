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
        private readonly string versionNumber;
        private readonly ExternalEvent externalEvent;
        private static readonly Process currentProcess = Process.GetCurrentProcess();
        private static readonly string docPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        public static object SyncLocker { get; set; } = new();


        public RevitExternalEventHandler(string version)
        {
            externalEvent = ExternalEvent.Create(this);
            Log.Logger = ConfigureLogger(version);
            versionNumber = version;
        }


        [STAThread]
        public void Execute(UIApplication uiapp)
        {
            currentProcess.PriorityBoostEnabled = true;

            AutomationHandler autoHandler = new(uiapp);

            uiapp.Idling += new EventHandler<IdlingEventArgs>(OnIdling);

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


        internal ILogger ConfigureLogger(string version)
        {
            return new LoggerConfiguration()
                .WriteTo.File(Path.Combine(docPath, $"RevitBIMTool {version}.txt"),
                    rollingInterval: RollingInterval.Day,
                    fileSizeLimitBytes: 10_000_000,
                    retainedFileCountLimit: 3)
                .MinimumLevel.Debug()
                .CreateLogger();
        }


        internal void OnIdling(object sender, IdlingEventArgs e)
        {
            Log.Debug($"Idling session called");

            if (sender is UIApplication uiapp)
            {
                CloseRevitApplication(uiapp);
            }
        }


        internal void CloseRevitApplication(UIApplication uiapp)
        {
            try
            {
                Log.Warning("Сlose Revit ...");
                uiapp.Application.PurgeReleasedAPIObjects();
                uiapp.Idling -= new EventHandler<IdlingEventArgs>(OnIdling);
            }
            finally
            {
                Thread.Sleep(1000);
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
            return externalEvent.Raise();
        }

    }

}

