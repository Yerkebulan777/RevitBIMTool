using Autodesk.Revit.DB;


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



}
