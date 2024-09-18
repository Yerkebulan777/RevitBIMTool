using Autodesk.Revit.UI;
using RevitBIMTool.ExportHandlers;
using RevitBIMTool.Utils;
using Serilog;
using ServiceLibrary.Models;


namespace RevitBIMTool.Core
{
    internal sealed class RevitExternalEventHandler : IExternalEventHandler
    {
        private int counter;
        private readonly string versionNumber;
        private readonly ExternalEvent externalEvent;


        public RevitExternalEventHandler(string version, int length)
        {
            externalEvent = ExternalEvent.Create(this);
            versionNumber = version;
            counter = length;
        }


        public void Execute(UIApplication uiapp)
        {
            RevitActionHandler handler = new();

            SynchronizationContext context = SynchronizationContext.Current;

            TaskRequestContainer requestContainer = TaskRequestContainer.Instance;

            while (requestContainer.PopTaskModel(versionNumber, out TaskRequest model))
            {
                if (GeneralTaskHandler.IsValidTask(ref model, out string output))
                {
                    LoggerHelper.SetupLogger(context, model.RevitFileName);

                    SynchronizationContext.SetSynchronizationContext(context);

                    output += handler.RunDocumentAction(uiapp, model, GeneralTaskHandler.RunTask);

                    Log.Information($"Task result: \t{output}");

                    if (RevitFileHelper.IsCountOut(ref counter))
                    {
                        RevitFileHelper.CloseRevitApplication();
                    }
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

