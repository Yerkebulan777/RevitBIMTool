﻿using Autodesk.Revit.UI;
using RevitBIMTool.ExportHandlers;
using RevitBIMTool.Utils;
using Serilog;
using ServiceLibrary.Models;


namespace RevitBIMTool.Core
{
    internal sealed class RevitExternalEventHandler : IExternalEventHandler
    {
        private readonly DateTime startTime;
        private readonly string versionNumber;
        private readonly ExternalEvent externalEvent;

        public RevitExternalEventHandler(string version)
        {
            externalEvent = ExternalEvent.Create(this);
            startTime = DateTime.Now;
            versionNumber = version;
        }


        public void Execute(UIApplication uiapp)
        {
            RevitActionHandler handler = new();

            SynchronizationContext context = SynchronizationContext.Current;

            TaskRequestContainer requestContainer = TaskRequestContainer.Instance;

            while (requestContainer.PopTaskModel(versionNumber, out TaskRequest model))
            {
                if (GeneralTaskHandler.IsValidTask(ref model))
                {
                    LoggerHelper.SetupLogger(context, model);

                    SynchronizationContext.SetSynchronizationContext(context);

                    string output = handler.RunDocumentAction(uiapp, model, GeneralTaskHandler.RunTask);

                    Log.Information($"Task result:\t{output}");

                    if (RevitFileHelper.IsTimedOut(startTime))
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

