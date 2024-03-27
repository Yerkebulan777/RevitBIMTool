using Autodesk.Revit.UI;
using CommunicationService;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Threading.Tasks;


namespace RevitBIMTool.Core
{
    public sealed class RevitExternalEventHandler : IExternalEventHandler
    {
        private readonly long taskBotChatId;
        private readonly ExternalEvent externalEvent;
        private static readonly object syncLocker = new object();
        private readonly Process currentProcess = Process.GetCurrentProcess();


        public RevitExternalEventHandler(long botChatId)
        {
            externalEvent = ExternalEvent.Create(this);
            taskBotChatId = botChatId;
        }


        public void Execute(UIApplication uiapp)
        {
            currentProcess.PriorityBoostEnabled = true;
            AutomationHandler autoHandler = new AutomationHandler(uiapp);
            Thread.CurrentThread.CurrentCulture = CultureInfo.InvariantCulture;
            
            while (TaskRequestContainer.Instance.PopTaskModel(taskBotChatId, out TaskRequest taskRequest))
            {
                string revitName = Path.GetFileNameWithoutExtension(taskRequest.RevitFilePath);

                lock (syncLocker)
                {
                    Debug.WriteLine(revitName);

                    if (taskBotChatId.Equals(taskRequest.BotChatId))
                    {
                        string output = autoHandler.ExecuteTask(taskRequest);

                        Task task = new Task(async () =>
                        {
                            await RevitMessageManager.SendInfoAsync(taskBotChatId, output);
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


