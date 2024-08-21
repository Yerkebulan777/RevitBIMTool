using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Serilog;
using System.Diagnostics;


namespace RevitBIMTool.Utils;
internal static class RevitFileHelper
{

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


    public static void ClosePreviousDocument(UIApplication uiapp, ref Document doc)
    {
        try
        {
            if (doc is not null && doc.IsValidObject && doc.Close(false))
            {
                uiapp.Application.PurgeReleasedAPIObjects();
                doc?.Dispose();
            }
        }
        finally
        {
            doc = uiapp.ActiveUIDocument.Document;
        }
    }


    public static void CloseRevitApplication()
    {
        Process currentProcess = Process.GetCurrentProcess();
        Log.Warning($"Start Close Revit Application");
        if (!currentProcess.HasExited)
        {
            Log.CloseAndFlush();
            currentProcess?.Kill();
            currentProcess?.Dispose();
        }
    }

}
