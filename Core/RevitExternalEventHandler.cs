using Autodesk.Revit.UI;
using Serilog;
using ServiceLibrary.Models;
using System.Globalization;
using System.IO;


namespace RevitBIMTool.Core
{
    public sealed class RevitExternalEventHandler : IExternalEventHandler
    {
        private readonly string versionNumber;
        private readonly ExternalEvent externalEvent;

        public static object SyncLocker { get; } = new();


        public RevitExternalEventHandler(string version)
        {
            externalEvent = ExternalEvent.Create(this);
            versionNumber = version;
        }


        [STAThread]
        public void Execute(UIApplication uiapp)
        {
            AutomationHandler autoHandler = new(uiapp);

            Thread.CurrentThread.CurrentCulture = CultureInfo.InvariantCulture;

            while (TaskRequestContainer.Instance.PopTaskModel(versionNumber, out TaskRequest taskRequest))
            {
                if (File.Exists(taskRequest.RevitFilePath))
                {
                    string output = autoHandler.ExecuteTask(taskRequest);

                    RevitMessageManager.SendInfo(taskRequest.ChatId, output);

                    Log.Information($"Task result:\r\n\t{output}");
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

