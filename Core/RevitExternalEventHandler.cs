using Autodesk.Revit.UI;
using CommunicationService;
using CommunicationService.Models;
using System.Diagnostics;
using System.Globalization;
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
            currentProcess.PriorityBoostEnabled = true;
            AutomationHandler autoHandler = new(uiapp);

            while (TaskRequestContainer.Instance.PopTaskModel(versionNumber, out TaskRequest taskRequest))
            {
                string revitName = Path.GetFileNameWithoutExtension(taskRequest.RevitFilePath);

                lock (syncLocker)
                {
                    Debug.WriteLine(revitName);
                    long taskBotChatId = taskRequest.BotChatId;
                    string output = autoHandler.ExecuteTask(taskRequest);

                    Task task = new(async () =>
                    {
                        await RevitMessageManager.SendInfoAsync(taskBotChatId, output);
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


