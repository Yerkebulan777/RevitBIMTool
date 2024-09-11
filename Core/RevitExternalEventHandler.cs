﻿using Autodesk.Revit.UI;
using RevitBIMTool.ExportHandlers;
using RevitBIMTool.Utils;
using Serilog;
using ServiceLibrary.Models;
using System.IO;


namespace RevitBIMTool.Core
{
    public sealed class RevitExternalEventHandler : IExternalEventHandler
    {
        private readonly DateTime startTime;
        private readonly string versionNumber;
        private readonly ExternalEvent externalEvent;
        private static readonly string myDocuments = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);


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
                Log.Logger = ConfigureLogger(model);

                if (GeneralTaskHandler.IsValidTask(ref model))
                {
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


        internal ILogger ConfigureLogger(TaskRequest request)
        {
            string logDir = Path.Combine(myDocuments, $"RevitBIMTool");
            string logName = $"{request.RevitFileName}[{request.CommandNumber}].txt";
            string logPath = Path.Combine(logDir, logName);
            RevitPathHelper.EnsureDirectory(logDir);
            RevitPathHelper.DeleteExistsFile(logPath);

            if (Log.Logger != null)
            {
                Thread.Sleep(100);
                Log.CloseAndFlush();
            }

            return new LoggerConfiguration()
                .WriteTo.File(logPath)
                .MinimumLevel.Debug()
                .CreateLogger();
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

