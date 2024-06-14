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

        public static readonly object SyncLocker = new();

        public RevitExternalEventHandler(string version)
        {
            externalEvent = ExternalEvent.Create(this);
            versionNumber = version;
        }


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
                        await RevitMessageManager.SendInfoAsync(taskRequest.BotChatId, result);
                    });

                    task.RunSynchronously();
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
                    Log.Warning("Start purge api objects ...");
                    uiapp.Application.PurgeReleasedAPIObjects();
                    Log.Warning("Start closing the Revit ...");
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


