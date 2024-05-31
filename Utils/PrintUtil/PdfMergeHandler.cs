﻿using iTextSharp.text.pdf;
using RevitBIMTool.Model;
using Serilog;
using System.IO;
using Document = iTextSharp.text.Document;


namespace RevitBIMTool.Utils.PrintUtil;
internal static class PdfMergeHandler
{

    public static void CombinePDFsFromFolder(List<SheetModel> sheetModels, string directory, string outputFullName, bool deleted = true)
    {
        if (File.Exists(outputFullName)) { File.Delete(outputFullName); }

        using FileStream stream = new(outputFullName, FileMode.Create);

        Document outputDocument = new();

        PdfCopy copy = new PdfSmartCopy(outputDocument, stream);

        outputDocument.Open();

        foreach (SheetModel model in SheetModel.SortSheetModels(sheetModels))
        {
            Log.Debug($"Sheet name: {model.SheetFullName} in {directory}");
            Log.Debug($"Organization group name: {model.OrganizationGroupName}");
            Log.Debug($"Sheet number: {model.StringNumber} ({model.DigitNumber})");

            string filePath = SheetModel.FindFileInDirectory(directory, model.SheetFullName);

            if (!string.IsNullOrEmpty(filePath))
            {
                PdfReader reader = new(filePath);
                reader.ConsolidateNamedDestinations();

                try
                {
                    for (int num = 1; num <= reader.NumberOfPages; num++)
                    {
                        PdfImportedPage page = copy.GetImportedPage(reader, num);

                        if (page != null && outputDocument.IsOpen())
                        {
                            copy.AddPage(page);
                        }
                    }

                    copy.FreeReader(reader);

                }
                catch (Exception ex)
                {
                    Log.Error(ex, ex.Message);
                }
                finally
                {
                    reader.Close();
                    model.Dispose();

                    if (deleted)
                    {
                        File.Delete(filePath);
                    }
                }
            }
        }

        copy.Close();
        outputDocument.Close();
    }


}