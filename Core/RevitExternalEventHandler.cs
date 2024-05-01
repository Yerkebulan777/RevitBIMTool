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
        private static readonly object syncLocker = new();
        private readonly Process currentProcess = Process.GetCurrentProcess();


        public RevitExternalEventHandler(string version)
        {
            externalEvent = ExternalEvent.Create(this);
            versionNumber = version;
        }


        public void Execute(UIApplication uiapp)
        {
            Log.Information("Start execute ... ");
            currentProcess.PriorityBoostEnabled = true;
            AutomationHandler autoHandler = new(uiapp);

            while (TaskRequestContainer.Instance.PopTaskModel(versionNumber, out TaskRequest taskRequest))
            {
                lock (syncLocker)
                {
                    if (File.Exists(taskRequest.RevitFilePath))
                    {
                        Log.Information($"Run {taskRequest.RevitFilePath}");
                        string result = autoHandler.ExecuteTask(taskRequest);

                        Task task = new(async () =>
                        {
                            await Task.Delay(taskRequest.CommandNumber * 1000);
                            await RevitMessageManager.SendInfoAsync(taskRequest.BotChatId, result);
                        });

                        task.RunSynchronously();
                    }
                }
            }

            CloseRevitApplication(uiapp);
        }


        private void CloseRevitApplication(UIApplication uiapp)
        {
            if (!currentProcess.HasExited)
            {
                try
                {
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
            return externalEvent.Raise();
        }


    }

}


