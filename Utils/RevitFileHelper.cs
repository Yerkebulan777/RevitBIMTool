using Autodesk.Revit.DB;
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

    public static void SaveAsBIM(Document doc, string filePath)
    {
        if (File.Exists(filePath))
        {
            string directoryName = Path.GetDirectoryName(filePath);
            string bimDirectoryPath = Path.Combine(directoryName, "BIM");
            if (!Directory.Exists(bimDirectoryPath))
            {
                _ = Directory.CreateDirectory(bimDirectoryPath);
            }

            string fileName = Path.GetFileName(filePath);
            WorksharingSaveAsOptions options = new();
            string newFilePath = Path.Combine(bimDirectoryPath, fileName);
            RevitFileHelper.SaveAs(doc, newFilePath, options, 50);
        }
    }

}
