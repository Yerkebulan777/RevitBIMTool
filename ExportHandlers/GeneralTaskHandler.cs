using Autodesk.Revit.UI;
using RevitBIMTool.Utils;
using Serilog;
using ServiceLibrary.Models;
using System.IO;
using System.Text;


namespace RevitBIMTool.ExportHandlers
{
    internal static class GeneralTaskHandler
    {

        private static readonly object syncLocker = new();

        public static bool IsValidTask(ref TaskRequest model)
        {
            bool result = false;

            string revitFilePath = model.RevitFilePath;
            string revitFileName = model.RevitFileName;

            lock (syncLocker)
            {
                switch (model.CommandNumber)
                {
                    case 1: // PDF

                        model.ExportFolder = ExportHelper.SetDirectory(revitFilePath, "03_PDF", true);
                        model.ExportBaseFile = Path.Combine(model.ExportFolder, $"{revitFileName}.pdf");
                        if (!ExportHelper.IsFileUpdated(model.ExportBaseFile, revitFilePath))
                        {
                            RevitPathHelper.DeleteExistsFile(model.ExportBaseFile);
                            result = true;
                        }
                        break;

                    case 2: // DWG

                        model.ExportFolder = ExportHelper.SetDirectory(revitFilePath, "02_DWG", true);
                        model.ExportBaseFile = Path.Combine(model.ExportFolder, $"{revitFileName}.zip");
                        if (!ExportHelper.IsFileUpdated(model.ExportBaseFile, revitFilePath))
                        {
                            RevitPathHelper.DeleteExistsFile(model.ExportBaseFile);
                            result = true;
                        }
                        break;

                    case 3: // NWC

                        model.ExportFolder = ExportHelper.SetDirectory(revitFilePath, "05_NWC", false);
                        model.ExportBaseFile = Path.Combine(model.ExportFolder, $"{revitFileName}.nwc");
                        if (!ExportHelper.IsFileUpdated(model.ExportBaseFile, revitFilePath))
                        {
                            RevitPathHelper.DeleteExistsFile(model.ExportBaseFile);
                            result = true;
                        }
                        break;

                }
            }

            return result;
        }


        public static string RunTask(UIDocument uidoc, TaskRequest model)
        {
            StringBuilder sb = new();

            _ = sb.AppendLine($"Command: [{model.CommandNumber}]");
            _ = sb.AppendLine($"File name: {model.RevitFileName}");
            _ = sb.AppendLine($"Directory: {model.ExportFolder}");

            Log.Debug($"Start task {sb}");

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
