﻿using Autodesk.Revit.UI;
using RevitBIMTool.Utils.Common;
using Serilog;
using ServiceLibrary.Models;
using System.IO;
using System.Text;


namespace RevitBIMTool.ExportHandlers
{
    internal static class TaskExportHandler
    {

        private static readonly object syncLocker = new();

        public static bool IsValidTask(ref TaskRequest model)
        {
            bool result = false;

            lock (syncLocker)
            {
                string revitFilePath = model.RevitFilePath;
                string revitFileName = model.RevitFileName;

                switch (model.CommandNumber)
                {
                    case 1: // PDF

                        model.ExportFolder = CommonExportManager.SetDirectory(revitFilePath, "03_PDF", true);
                        model.ExportBaseFile = Path.Combine(model.ExportFolder, $"{revitFileName}.pdf");
                        if (!FileValidator.IsUpdated(model.ExportBaseFile, revitFilePath))
                        {
                            PathHelper.DeleteExistsFile(model.ExportBaseFile);
                            Thread.Sleep(model.CommandNumber * 1000);
                            result = true;
                        }
                        break;

                    case 2: // DWG

                        model.ExportFolder = CommonExportManager.SetDirectory(revitFilePath, "02_DWG", true);
                        model.ExportBaseFile = Path.Combine(model.ExportFolder, $"{revitFileName}.zip");
                        if (!FileValidator.IsUpdated(model.ExportBaseFile, revitFilePath))
                        {
                            PathHelper.DeleteExistsFile(model.ExportBaseFile);
                            Thread.Sleep(model.CommandNumber * 1000);
                            result = true;
                        }
                        break;

                    case 3: // NWC

                        model.ExportFolder = CommonExportManager.SetDirectory(revitFilePath, "05_NWC", false);
                        model.ExportBaseFile = Path.Combine(model.ExportFolder, $"{revitFileName}.nwc");
                        if (!FileValidator.IsUpdated(model.ExportBaseFile, revitFilePath))
                        {
                            PathHelper.DeleteExistsFile(model.ExportBaseFile);
                            Thread.Sleep(model.CommandNumber * 1000);
                            result = true;
                        }
                        break;

                    default:
                        break;

                }
            }

            return result;
        }

        public static string RunTask(UIDocument uidoc, TaskRequest model)
        {
            StringBuilder sb = new();

            _ = sb.AppendLine("Start task... ");
            _ = sb.AppendLine($"Command: [{model.CommandNumber}]");
            _ = sb.AppendLine($"File name: {model.RevitFileName}");
            _ = sb.AppendLine($"Directory: {model.ExportFolder}");

            Log.Information(sb.ToString());

            switch (model.CommandNumber)
            {
                case 1: // PDF
                    ExportPdfProcessor.Execute(uidoc, model.RevitFilePath, model.ExportFolder);
                    break;

                case 2: // DWG

                    ExportDwgProcessor.Execute(uidoc, model.RevitFilePath, model.ExportFolder);
                    break;

                case 3: // NWC

                    ExportNwcProcessor.Execute(uidoc, model.RevitFilePath, model.ExportFolder);
                    break;

                default:

                    break;
            }

            return sb.ToString();
        }

    }

}
