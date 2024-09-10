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
public sealed class RevitTaskHandler
{
    private Document document;
    private const int waitTimeout = 1000;
    private readonly UIApplication uiapp;


    public RevitTaskHandler(UIApplication application)
    {
        uiapp = application;
    }


    #region Methods

    public string RunTask(UIDocument uidoc, TaskRequest model)
    {
        string revitFilePath = model.RevitFilePath;
        string revitFileName = model.RevitFileName;
        Log.Debug($"{revitFileName} [{model.CommandNumber}]");

        switch (model.CommandNumber)
        {
            case 1: // PDF

                model.ExportFolder = ExportHelper.SetDirectory(revitFilePath, "03_PDF", true);
                model.TargetFullPath = Path.Combine(model.ExportFolder, $"{revitFileName}.pdf");
                if (!ExportHelper.IsTargetFileUpdated(model.TargetFullPath, revitFilePath))
                {
                    return ExportToPDFHandler.ExportToPDF(uidoc, model.RevitFilePath, model.ExportFolder);
                }

                break;

            case 2: // DWG

                model.ExportFolder = ExportHelper.SetDirectory(revitFilePath, "02_DWG", true);
                model.TargetFullPath = Path.Combine(model.ExportFolder, $"{revitFileName}.zip");
                if (!ExportHelper.IsTargetFileUpdated(model.TargetFullPath, revitFilePath))
                {
                    return ExportToDWGHandler.ExportExecute(uidoc, model.RevitFilePath, model.ExportFolder);
                }

                break;

            case 3: // NWC

                model.ExportFolder = ExportHelper.SetDirectory(revitFilePath, "05_NWC", false);
                model.TargetFullPath = Path.Combine(model.ExportFolder, $"{revitFileName}.nwc");
                if (!ExportHelper.IsTargetFileUpdated(model.TargetFullPath, revitFilePath))
                {
                    return ExportToNWCHandler.ExportToNWC(uidoc, model.RevitFilePath, model.ExportFolder);
                }

                break;

            default:

                return $"Failed command: {model.CommandNumber}";
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

