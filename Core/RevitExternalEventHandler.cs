using Autodesk.Revit.UI;
using Serilog;
using ServiceLibrary.Helpers;
using ServiceLibrary.Models;
using System.Globalization;


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
                Log.Information($"Started command: ({taskRequest.CommandNumber}) file name: {taskRequest.RevitFileName}");

                if (PathHelper.IsFileAccessible(taskRequest.RevitFilePath, out string output))
                {
                    output = autoHandler.ExecuteTask(taskRequest);
                    Log.Information($"Task result:\r\n\t{output}");
                }

                MessageManager.SendInfo(taskRequest.ChatId, output);
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

