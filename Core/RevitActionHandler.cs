﻿using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using RevitBIMTool.Utils;
using ServiceLibrary.Models;
using System.Diagnostics;
using System.Text;
using Document = Autodesk.Revit.DB.Document;


namespace RevitBIMTool.Core;
internal sealed class RevitActionHandler
{
    private Document document;


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
        lock (uiapp)
        {
            OpenOptions openOptions = new()
            {
                DetachFromCentralOption = DetachFromCentralOption.DetachAndPreserveWorksets,
                Audit = true,
            };

            openOptions.SetOpenWorksetsConfiguration(new WorksetConfiguration(WorksetConfigurationOption.OpenAllWorksets));
            ModelPath modelPath = ModelPathUtils.ConvertUserVisiblePathToModelPath(taskModel.RevitFilePath);
            UIDocument uidoc = uiapp.OpenAndActivateDocument(modelPath, openOptions, false);
            RevitFileHelper.ClosePreviousDocument(uiapp, ref document);
            RevitLinkHelper.CheckAndRemoveUnloadedLinks(document);
        }

        return revitAction(uiapp.ActiveUIDocument, taskModel);
    }


    private string WithErrorReportingAndHandling(UIApplication uiapp, Func<string> revitAction)
    {
        string WithOpeningErrorReporting()
        {
            StringBuilder sb = new();
            Stopwatch stopwatch = new();
            stopwatch.Start();

            try
            {
                _ = sb.AppendLine(revitAction());
            }
            catch (Exception ex)
            {
                _ = sb.AppendLine(ex.Message);
            }
            finally
            {
                stopwatch.Stop();
            }

            _ = sb.AppendLine($"[{stopwatch.Elapsed:h\\:mm\\:ss}]");

            return sb.ToString();
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

}

