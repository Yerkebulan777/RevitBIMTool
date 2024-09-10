using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using RevitBIMTool.ExportHandlers;
using RevitBIMTool.Utils;
using Serilog;
using ServiceLibrary.Models;
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


    public string RunExecuteTask(TaskRequest taskRequest, SynchronizationContext context)
    {
        builder = new StringBuilder();

        DateTime startedTime = DateTime.Now;

        SynchronizationContext.SetSynchronizationContext(context);

        string output = RunDocumentAction(uiapp, taskRequest, RunTask);

        string formattedTime = (DateTime.Now - startedTime).ToString(@"h\:mm\:ss");

        _ = builder.Append($"{taskRequest.RevitFileName}");
        _ = builder.Append($"[{formattedTime}]");
        _ = builder.AppendLine(output);

        return builder.ToString();
    }


    private string RunTask(UIDocument uidoc, TaskRequest taskModel)
    {
        StringBuilder sb = new();

        Log.Debug($"{taskModel.RevitFileName} [{taskModel.CommandNumber}]");

        string sectionName = RevitPathHelper.GetSectionName(taskModel.RevitFilePath);

        sb = taskModel.CommandNumber switch
        {
            1 => sb.AppendLine(ExportToPDFHandler.ExportToPDF(uidoc, taskModel.RevitFilePath, sectionName)),
            2 => sb.AppendLine(ExportToDWGHandler.ExportExecute(uidoc, taskModel.RevitFilePath, sectionName)),
            3 => sb.AppendLine(ExportToNWCHandler.ExportToNWC(uidoc, taskModel.RevitFilePath, sectionName)),
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

        lock (uiapp)
        {
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
                RevitLinkHelper.CheckAndRemoveUnloadedLinks(uidoc.Document);
                RevitFileHelper.ClosePreviousDocument(uiapp, ref document);
                output = output.AppendLine(revitAction(uidoc, taskModel));
            }
        }

        return output.ToString();
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
            result = ex.Message;
            Log.Fatal(result);
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

