﻿using Autodesk.Revit.DB;
using RevitBIMTool.Utils;
using System.IO;


namespace RevitBIMTool.ExportHandlers;
internal class AutoHelper
{
    public void SaveAsBIM(Document doc, string filePath)
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
            WorksharingSaveAsOptions options = new WorksharingSaveAsOptions();
            string newFilePath = Path.Combine(bimDirectoryPath, fileName);
            RevitFileHelper.SaveAs(doc, newFilePath, options, 50);
        }
    }
}
