﻿using Autodesk.Revit.UI;
using RevitBIMTool.ExportHandlers;
using RevitBIMTool.Utils;
using RevitBIMTool.Utils.Common;
using Serilog;
using ServiceLibrary.Models;


namespace RevitBIMTool.Core
{
    internal sealed class RevitExternalEventHandler : IExternalEventHandler
    {
        private DateTime startTime;
        private readonly string versionNumber;
        private readonly ExternalEvent externalEvent;


        public RevitExternalEventHandler(string version, int length)
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
                if (GeneralTaskHandler.IsValidTask(ref model, out string output))
                {
                    LoggerHelper.SetupLogger(context, model.RevitFileName);

                    SynchronizationContext.SetSynchronizationContext(context);

                    string result = handler.RunDocumentAction(uiapp, model, GeneralTaskHandler.RunTask);

                    Log.Information($" \n Result: {result} ");

                    if (RevitFileHelper.IsTimeOut(ref startTime))
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

