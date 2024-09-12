﻿using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Serilog;
using System.Diagnostics;


namespace RevitBIMTool.Utils;
internal static class RevitFileHelper
{
    public static bool IsTimedOut(DateTime startTime, int timeout = 100)
    {
        bool result = false;

        TimeSpan maximum = TimeSpan.FromMinutes(timeout);

        if ((DateTime.Now - startTime) > maximum)
        {
            Log.Debug("Time limit reached");
            Thread.Sleep(1000);
            result = true;
        }

        return result;
    }


    public static void SaveAs(Document doc, string filePath, WorksharingSaveAsOptions options = null, int maxBackups = 25)
    {
        ModelPath modelPathObj = ModelPathUtils.ConvertUserVisiblePathToModelPath(filePath);

        SaveAsOptions saveAsOptions = new()
        {
            Compact = true,
            OverwriteExistingFile = true
        };

        if (options != null)
        {
            options.SaveAsCentral = true;
            saveAsOptions.SetWorksharingOptions(options);
            saveAsOptions.MaximumBackups = maxBackups;
        }

        doc.SaveAs(modelPathObj, saveAsOptions);
    }


    public static void ClosePreviousDocument(UIApplication uiapp, ref Document document)
    {
        try
        {
            if (document != null && document.IsValidObject && document.Close(false))
            {
                uiapp.Application.PurgeReleasedAPIObjects();
            }
        }
        finally
        {
            document = uiapp.ActiveUIDocument.Document;
        }
    }


    public static void CloseRevitApplication()
    {
        Process currentProcess = Process.GetCurrentProcess();

        try
        {
            Thread.Sleep(1000);
            Log.Debug("Closed Revit...");
            Log.CloseAndFlush();
        }
        finally
        {
            currentProcess?.Kill();
            currentProcess?.Dispose();
        }

    }

}
