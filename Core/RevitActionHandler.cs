using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using RevitBIMTool.ExportHandlers;
using RevitBIMTool.Utils;
using Serilog;
using ServiceLibrary.Models;
using System.IO;
using Document = Autodesk.Revit.DB.Document;


namespace RevitBIMTool.Core;
public sealed class RevitActionHandler
{
    private Document document;
    private const int waitTimeout = 1000;
    private readonly UIApplication uiapp;


    public RevitActionHandler(UIApplication application)
    {
        uiapp = application;
    }


    #region Methods

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

