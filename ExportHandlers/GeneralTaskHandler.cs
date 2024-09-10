using Autodesk.Revit.UI;
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
                    model.TargetFullPath = Path.Combine(model.ExportFolder, $"{revitFileName}.pdf");
                    return !ExportHelper.IsTargetFileUpdated(model.TargetFullPath, revitFilePath);

                case 2: // DWG

                    model.ExportFolder = ExportHelper.SetDirectory(revitFilePath, "02_DWG", true);
                    model.TargetFullPath = Path.Combine(model.ExportFolder, $"{revitFileName}.zip");
                    return !ExportHelper.IsTargetFileUpdated(model.TargetFullPath, revitFilePath);

                case 3: // NWC

                    model.ExportFolder = ExportHelper.SetDirectory(revitFilePath, "05_NWC", false);
                    model.TargetFullPath = Path.Combine(model.ExportFolder, $"{revitFileName}.nwc");
                    return !ExportHelper.IsTargetFileUpdated(model.TargetFullPath, revitFilePath);

                default: return false;
            }

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

                    ExportToPDFHandler.ExportToPDF(uidoc, model.RevitFilePath, model.ExportFolder);
                    break;

                case 2: // DWG

                    ExportToDWGHandler.ExportExecute(uidoc, model.RevitFilePath, model.ExportFolder);
                    break;

                case 3: // NWC

                    ExportToNWCHandler.ExportToNWC(uidoc, model.RevitFilePath, model.ExportFolder);
                    break;

                default:

                    break;
            }

            return sb.ToString();
        }

    }

}
