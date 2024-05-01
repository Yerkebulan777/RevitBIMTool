using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using CommunicationService.Models;
using RevitBIMTool.Utils;
using System.IO;
using System.Text;
using Document = Autodesk.Revit.DB.Document;


namespace RevitBIMTool.Core;
public sealed class AutomationHandler
{
    private Document document;
    private const int waitTimeout = 1000;
    private readonly UIApplication uiapp;
    private StringBuilder builder = new();
    private static readonly object syncLocker = new();


    public AutomationHandler(UIApplication application)
    {
        uiapp = application;
    }


    [STAThread]
    public string ExecuteTask(TaskRequest taskRequest)
    {
        builder = new StringBuilder();
        DateTime startedTime = DateTime.Now;
        string sourceFilePath = taskRequest.RevitFilePath;

        if (File.Exists(sourceFilePath))
        {
            string output = RunDocumentAction(uiapp, taskRequest, RunTaskByNumber);

            lock (syncLocker)
            {
                TimeSpan elapsedTime = DateTime.Now - startedTime;
                string fileName = Path.GetFileNameWithoutExtension(sourceFilePath);
                string formattedTime = elapsedTime.ToString(@"h\:mm\:ss");
                _ = builder.AppendLine($"{fileName}  [{formattedTime}]");
                _ = builder.AppendLine(output);
            }
        }

        return builder.ToString();
    }


    private string RunTaskByNumber(Document doc, TaskRequest taskModel)
    {
        StringBuilder sb = new();

        RevitLinkHelper.CheckAndRemoveUnloadedLinks(doc);

        sb = taskModel.CommandNumber switch
        {
            1 => sb.AppendLine(ExportToPDFHandler.ExportToPDF(doc, taskModel.RevitFilePath)),
            2 => sb.AppendLine(ExportToDWGHandler.ExportToDWGAsync(doc, taskModel.RevitFilePath)),
            3 => sb.AppendLine(ExportToNWCHandler.ExportToNWC(doc, taskModel.RevitFilePath)),
            _ => sb.AppendLine($"Failed command: {taskModel.CommandNumber}"),
        };

        return sb.ToString();
    }


    private string RunDocumentAction(UIApplication uiapp, TaskRequest taskModel, Func<Document, TaskRequest, string> revitAction)
    {
        string revitTaskAction()
        {
            return WithOpenedDocument(uiapp, taskModel, revitAction);
        }

        return WithErrorReportingAndHandling(uiapp, revitTaskAction);
    }


    private string WithOpenedDocument(UIApplication uiapp, TaskRequest taskModel, Func<Document, TaskRequest, string> revitAction)
    {
        UIDocument uidoc = null;
        StringBuilder output = new();
        OpenOptions openOptions = new()
        {
            DetachFromCentralOption = DetachFromCentralOption.DetachAndPreserveWorksets,
            Audit = true,
        };

        try
        {
            openOptions.SetOpenWorksetsConfiguration(new WorksetConfiguration(WorksetConfigurationOption.OpenAllWorksets));
            ModelPath modelPath = ModelPathUtils.ConvertUserVisiblePathToModelPath(taskModel.RevitFilePath);
            uidoc = uiapp.OpenAndActivateDocument(modelPath, openOptions, false);
        }
        catch (Exception ex)
        {
            throw new Exception("Error while opening the document", ex);
        }
        finally
        {
            if (ClosePreviousDocument(uidoc, ref document))
            {
                output = output.AppendLine(revitAction(document, taskModel));
            }
        }

        return output.ToString();
    }


    private bool ClosePreviousDocument(UIDocument uidoc, ref Document document)
    {
        bool result = false;

        if (uidoc != null)
        {
            lock (syncLocker)
            {
                try
                {
                    if (document is null)
                    {
                        result = true;
                    }
                    else if (document.IsValidObject)
                    {
                        result = document.Close(false);
                    }
                }
                finally
                {
                    if (uidoc.Document is Document doc)
                    {
                        document = doc;
                    }
                }
            }
        }

        return result;
    }


    private string WithErrorReportingAndHandling(UIApplication uiapp, Func<string> revitAction)
    {
        string revitTaskAction()
        {
            return WithOpeningErrorReporting(revitAction);
        }

        return WithAutomatedErrorHandling(uiapp, revitTaskAction);
    }


    private string WithOpeningErrorReporting(Func<string> documentOpeningAction)
    {
        string result = "Main thread context error";
        if (Monitor.TryEnter(syncLocker, waitTimeout))
        {
            try
            {
                result = documentOpeningAction();
            }
            catch (Exception ex)
            {
                result = $"Error: {ex.InnerException}\nMessage: {ex.Message}\n{ex.StackTrace}";
            }
            finally
            {
                Monitor.Exit(syncLocker);
            }
        }
        return result;
    }


    private string WithAutomatedErrorHandling(UIApplication uiapp, Func<string> revitAction)
    {
        string revitTaskAction()
        {
            return RevitDialogHanding.WithDialogBoxShowingHandler(uiapp, revitAction);
        }

        return FailuresHanding.WithFailuresProcessingHandler(uiapp.Application, revitTaskAction);
    }


}

