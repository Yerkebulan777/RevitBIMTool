using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using CommunicationService.Models;
using RevitBIMTool.ExportHandlers;
using RevitBIMTool.Utils;
using Serilog;
using System.Text;
using Document = Autodesk.Revit.DB.Document;


namespace RevitBIMTool.Core;
public sealed class AutomationHandler
{
    private Document document;
    private const int waitTimeout = 1000;
    private readonly UIApplication uiapp;
    private StringBuilder builder = new();


    public AutomationHandler(UIApplication application)
    {
        uiapp = application;
    }


    public string ExecuteTask(TaskRequest taskRequest)
    {
        builder = new StringBuilder();

        DateTime startedTime = DateTime.Now;

        Log.Debug($"Run file: {taskRequest.RevitFileName} ");

        string output = RunDocumentAction(uiapp, taskRequest, RunTaskByNumber);

        string formattedTime = (DateTime.Now - startedTime).ToString(@"h\:mm\:ss");

        _ = builder.Append($" {taskRequest.RevitFileName} ");
        _ = builder.Append($" [{formattedTime}] ");
        _ = builder.AppendLine(output);

        return builder.ToString();
    }


    private string RunTaskByNumber(UIDocument uidoc, TaskRequest taskModel)
    {
        StringBuilder sb = new();

        RevitLinkHelper.CheckAndRemoveUnloadedLinks(uidoc.Document);

        Log.Information($"Command number: {taskModel.CommandNumber}");

        sb = taskModel.CommandNumber switch
        {
            1 => sb.AppendLine(ExportToPDFHandler.ExportToPDF(uidoc, taskModel.RevitFilePath)),
            2 => sb.AppendLine(ExportToDWGHandler.ExportExecute(uidoc, taskModel.RevitFilePath)),
            3 => sb.AppendLine(ExportToNWCHandler.ExportToNWC(uidoc, taskModel.RevitFilePath)),
            _ => sb.AppendLine($"Failed command: {taskModel.CommandNumber}"),
        };

        return sb.ToString();
    }


    private string RunDocumentAction(UIApplication uiapp, TaskRequest taskModel, Func<UIDocument, TaskRequest, string> revitAction)
    {
        string revitTaskAction()
        {
            return WithOpenedDocument(uiapp, taskModel, revitAction);
        }

        return WithErrorReportingAndHandling(uiapp, revitTaskAction);
    }


    private string WithOpenedDocument(UIApplication uiapp, TaskRequest taskModel, Func<UIDocument, TaskRequest, string> revitAction)
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
            if (ClosePreviousDocument(uidoc))
            {
                output = output.AppendLine(revitAction(uidoc, taskModel));
            }
        }

        return output.ToString();
    }


    private bool ClosePreviousDocument(UIDocument uidoc)
    {
        bool result = false;

        if (uidoc != null)
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
                    Log.Warning("Start purge api objects ...");
                    uiapp.Application.PurgeReleasedAPIObjects();
                }
            }
            finally
            {
                Thread.Sleep(1000);
                document?.Dispose();
                document = uidoc.Document;
                Log.Information("Closed document");
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
        string result;

        try
        {
            result = documentOpeningAction();
        }
        catch (Exception ex)
        {
            result = $"\nError: {ex.Source} {ex.Message}\n{ex.StackTrace}";
            Log.Error(result);
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

