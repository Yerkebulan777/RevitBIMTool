﻿using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using CommunicationService.Models;
using RevitBIMTool.ExportHandlers;
using RevitBIMTool.Utils;
using Serilog;
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


    public string ExecuteTask(TaskRequest taskRequest)
    {
        builder = new StringBuilder();
        DateTime startedTime = DateTime.Now;
        string sourceFilePath = taskRequest.RevitFilePath;

        if (File.Exists(sourceFilePath))
        {
            Log.Information($"Run file: {Path.GetFileName(sourceFilePath)}");
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


    private string RunTaskByNumber(UIDocument uidoc, TaskRequest taskModel)
    {
        StringBuilder sb = new();
 
        RevitLinkHelper.CheckAndRemoveUnloadedLinks(uidoc.Document);

        Log.Information($"Command number: {taskModel.CommandNumber}");

        sb = taskModel.CommandNumber switch
        {
            1 => sb.AppendLine(ExportToPDFHandler.ExportToPDF(uidoc, taskModel.RevitFilePath)),
            2 => sb.AppendLine(ExportToDWGHandler.ExportToDWG(uidoc, taskModel.RevitFilePath)),
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
                        uiapp.Application.PurgeReleasedAPIObjects();
                    }
                }
                finally
                {
                    document?.Dispose();
                    document = uidoc.Document;
                    Log.Information("Closed document");
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
                result = $"\nError: {ex.Source} {ex.Message}\n{ex.StackTrace}";
                Log.Error(result);
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

