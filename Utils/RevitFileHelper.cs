﻿using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Serilog;
using System.Diagnostics;


namespace RevitBIMTool.Utils;
internal static class RevitFileHelper
{
    public static bool IsCountOut(ref int counter)
    {
        Log.Debug($"Counter: {counter}");

        Thread.Sleep(counter++);

        return counter > 100;
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
                Thread.Sleep(1000);
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
