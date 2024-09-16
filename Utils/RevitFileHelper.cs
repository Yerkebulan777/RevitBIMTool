using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Serilog;
using System.Diagnostics;


namespace RevitBIMTool.Utils;
internal static class RevitFileHelper
{
    public static bool IsTimedOut(in DateTime startTime, ref int counter)
    {
        Thread.Sleep(counter--);

        TimeSpan limit = TimeSpan.FromHours(3);

        if (counter > 100 || (DateTime.Now - startTime) > limit)
        {
            Log.Debug("Time limit reached"); return true;
        }

        return false;
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
