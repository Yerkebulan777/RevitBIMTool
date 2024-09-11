using Autodesk.Revit.UI;
using RevitBIMTool.Utils;
using ServiceLibrary.Models;
using System.IO;
using System.Text;


namespace RevitBIMTool.ExportHandlers
{
    internal static class GeneralTaskHandler
    {
        public static bool IsValidTask(ref TaskRequest model)
        {
            string revitFilePath = model.RevitFilePath;
            string revitFileName = model.RevitFileName;

            switch (model.CommandNumber)
            {
                case 1: // PDF

                    model.ExportFolder = ExportHelper.SetDirectory(revitFilePath, "03_PDF", true);
                    model.TargetPath = Path.Combine(model.ExportFolder, $"{revitFileName}.pdf");
                    if (!ExportHelper.IsTargetFileUpdated(model.TargetPath, revitFilePath))
                    {
                        RevitPathHelper.DeleteExistsFile(model.TargetPath);
                        return true;
                    }
                    break;

                case 2: // DWG

                    model.ExportFolder = ExportHelper.SetDirectory(revitFilePath, "02_DWG", true);
                    model.TargetPath = Path.Combine(model.ExportFolder, $"{revitFileName}.zip");
                    if (!ExportHelper.IsTargetFileUpdated(model.TargetPath, revitFilePath))
                    {
                        RevitPathHelper.DeleteExistsFile(model.TargetPath);
                        return true;
                    }
                    break;

                case 3: // NWC

                    model.ExportFolder = ExportHelper.SetDirectory(revitFilePath, "05_NWC", false);
                    model.TargetPath = Path.Combine(model.ExportFolder, $"{revitFileName}.nwc");
                    if (!ExportHelper.IsTargetFileUpdated(model.TargetPath, revitFilePath))
                    {
                        RevitPathHelper.DeleteExistsFile(model.TargetPath);
                        return true;
                    }
                    break;

            }

            return true;
        }


        public static string RunTask(UIDocument uidoc, TaskRequest model)
        {
            StringBuilder sb = new();

            _ = sb.Append($"[{model.CommandNumber}]");
            _ = sb.AppendLine(model.RevitFileName);
            _ = sb.AppendLine(model.ExportFolder);

            switch (model.CommandNumber)
            {
                case 1: // PDF

                    ExportToPDFHandler.Execute(uidoc, model.RevitFilePath, model.ExportFolder);
                    break;

                case 2: // DWG

                    ExportToDWGHandler.Execute(uidoc, model.RevitFilePath, model.ExportFolder);
                    break;

                case 3: // NWC

                    ExportToNWCHandler.Execute(uidoc, model.RevitFilePath, model.ExportFolder);
                    break;

                default:

                    break;
            }

            return sb.ToString();
        }

    }

}
