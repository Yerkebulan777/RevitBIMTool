using Autodesk.Revit.DB;
using System.Diagnostics;
using System.IO;


namespace RevitBIMTool.Utils;
internal static class RevitFileHelper
{
    public static void SaveAs(Document doc, string filePath, WorksharingSaveAsOptions worksharingSaveAsOptions = null, int maximumBackups = 25)
    {
        ModelPath modelPathObj = ModelPathUtils.ConvertUserVisiblePathToModelPath(filePath);

        SaveAsOptions saveAsOptions = new SaveAsOptions
        {
            Compact = true,
            OverwriteExistingFile = true
        };

        if (worksharingSaveAsOptions != null)
        {
            worksharingSaveAsOptions.SaveAsCentral = true;
            saveAsOptions.SetWorksharingOptions(worksharingSaveAsOptions);
            saveAsOptions.MaximumBackups = maximumBackups;
        }

        doc.SaveAs(modelPathObj, saveAsOptions);
    }


    public static void DeleteFileIfExists(string filePath)
    {
        if (File.Exists(filePath))
        {
            File.Delete(filePath);
        }
    }


    public static void OpenFolder(string folderPath)
    {
        Process[] processes = Process.GetProcessesByName("explorer");

        foreach (Process process in processes)
        {
            IntPtr handle = process.MainWindowHandle;
            string folderTitle = process.MainWindowTitle;
            if (handle == IntPtr.Zero || folderTitle.Contains(folderPath))
            {
                return;
            }
        }

        ProcessStartInfo startInfo = new ProcessStartInfo
        {
            FileName = "explorer.exe",
            Arguments = folderPath,
            UseShellExecute = true,
            Verb = "open"
        };

        _ = Process.Start(startInfo);
    }


}
