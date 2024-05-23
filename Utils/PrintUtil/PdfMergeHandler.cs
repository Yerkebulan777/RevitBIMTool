﻿using iTextSharp.text.pdf;
using RevitBIMTool.Model;
using System.Diagnostics;
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

        List<SheetModel> sheetModelList = SortSheetModels(sheetModels);

        foreach (SheetModel model in sheetModelList)
        {
            string filePath = model.FindFileInDirectory(directory);

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
                    Debug.WriteLine(ex);
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


    private static List<SheetModel> SortSheetModels(List<SheetModel> sheetModels)
    {
        sheetModels = sheetModels
        .OrderBy(sm => sm.SequenceNumber)
        .ThenBy(sm => sm.OrganizationGroupName)
        .ThenBy(sheetModel => sheetModel.SheetNumber).ToList();
        return sheetModels;
    }



}