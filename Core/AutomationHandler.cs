using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using RevitBIMTool.ExportHandlers;
using RevitBIMTool.Utils;
using Serilog;
using ServiceLibrary.Models;
using System.IO;
using System.Text;
using Document = Autodesk.Revit.DB.Document;


namespace RevitBIMTool.Core;
public sealed class AutomationHandler
{
    private Document document;
    private const int waitTimeout = 1000;
    private readonly UIApplication uiapp;


    public AutomationHandler(UIApplication application)
    {
        uiapp = application;
    }


    #region RevitTaskHandler

    public string RunTask(UIDocument uidoc, TaskRequest taskModel)
    {
        string revitFilePath = taskModel.RevitFilePath;
        string revitFileName = taskModel.RevitFileName;
        Log.Debug($"{revitFileName} [{taskModel.CommandNumber}]");
        string sectionName = RevitPathHelper.GetSectionName(taskModel.RevitFilePath);
        
        switch (taskModel.CommandNumber)
        {
            case 1: // PDF

                taskModel.ExportFolder = ExportHelper.SetDirectory(revitFilePath, "03_PDF", true);
                taskModel.ExportFullPath = Path.Combine(taskModel.ExportFolder, $"{revitFileName}.pdf");
                if (!ExportHelper.IsTargetFileUpdated(taskModel.ExportFullPath, revitFilePath))
                {
                    return ExportToPDFHandler.ExportToPDF(uidoc, taskModel.RevitFilePath, sectionName);
                }

                break;

            case 2: // DWG

                taskModel.ExportFolder = ExportHelper.SetDirectory(revitFilePath, "02_DWG", true);
                taskModel.ExportFullPath = Path.Combine(taskModel.ExportFolder, $"{revitFileName}.zip");
                if (!ExportHelper.IsTargetFileUpdated(taskModel.ExportFullPath, revitFilePath))
                {
                    return ExportToDWGHandler.ExportExecute(uidoc, taskModel.RevitFilePath, sectionName);
                }

                break;

            case 3: // NWC

                taskModel.ExportFolder = ExportHelper.SetDirectory(revitFilePath, "05_NWC", false);
                taskModel.ExportFullPath = Path.Combine(taskModel.ExportFolder, $"{revitFileName}.nwc");
                if (!ExportHelper.IsTargetFileUpdated(taskModel.ExportFullPath, revitFilePath))
                {
                    return ExportToNWCHandler.ExportToNWC(uidoc, taskModel.RevitFilePath, sectionName);
                }

                break;

            default:

                return $"Failed command: {taskModel.CommandNumber}";
        }

        return null;
    }


    public string RunDocumentAction(UIApplication uiapp, TaskRequest taskModel, Func<UIDocument, TaskRequest, string> revitAction)
    {
        string revitTaskAction()
        {
            return WithOpenedDocument(uiapp, taskModel, revitAction);
        }

        return WithErrorReportingAndHandling(uiapp, revitTaskAction);
    }


    private string WithOpenedDocument(UIApplication uiapp, TaskRequest taskModel, Func<UIDocument, TaskRequest, string> revitAction)
    {
        UIDocument uidoc;

        lock (uiapp)
        {
            OpenOptions openOptions = new()
            {
                DetachFromCentralOption = DetachFromCentralOption.DetachAndPreserveWorksets,
                Audit = true,
            };

            openOptions.SetOpenWorksetsConfiguration(new WorksetConfiguration(WorksetConfigurationOption.OpenAllWorksets));
            ModelPath modelPath = ModelPathUtils.ConvertUserVisiblePathToModelPath(taskModel.RevitFilePath);
            uidoc = uiapp.OpenAndActivateDocument(modelPath, openOptions, false);
            RevitFileHelper.ClosePreviousDocument(uiapp, ref document);
            RevitLinkHelper.CheckAndRemoveUnloadedLinks(document);
        }

        return revitAction(uidoc, taskModel);
    }


    private string WithErrorReportingAndHandling(UIApplication uiapp, Func<string> revitAction)
    {
        string WithOpeningErrorReporting()
        {
            string result = string.Empty;

            try
            {
                result = revitAction();
            }
            catch (Exception ex)
            {
                result += ex.Message;
                Log.Fatal(ex, result);
            }

            return result;
        }

        return WithAutomatedErrorHandling(uiapp, WithOpeningErrorReporting);
    }


    private string WithAutomatedErrorHandling(UIApplication uiapp, Func<string> revitAction)
    {
        string revitTaskAction()
        {
            return RevitDialogHanding.WithDialogBoxShowingHandler(uiapp, revitAction);
        }

        return FailuresHanding.WithFailuresProcessingHandler(uiapp.Application, revitTaskAction);
    }

    #endregion

}

