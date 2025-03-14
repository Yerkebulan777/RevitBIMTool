using iTextSharp.text.pdf;
using RevitBIMTool.Models;
using RevitBIMTool.Utils.Common;
using Serilog;
using System.IO;
using Document = iTextSharp.text.Document;

namespace RevitBIMTool.Utils.ExportPDF;

internal static class MergeHandler
{
    public static void Combine(List<SheetModel> sheetModels, string outputFullName, bool deleteOriginals = true)
    {
        List<SheetModel> validSheets = sheetModels?.Where(s => s.IsSuccessfully).ToList();

        Log.Information($"Combining {validSheets?.Count ?? 0} sheets");

        RevitPathHelper.DeleteExistsFile(outputFullName);

        if (validSheets is not null && validSheets.Any())
        {
            using Document outputDocument = new();
            using FileStream stream = new(outputFullName, FileMode.Create);
            using PdfCopy copy = new PdfSmartCopy(outputDocument, stream);

            outputDocument.Open();
            int totalPages = 0;

            foreach (SheetModel model in SheetHelper.SortSheetModels(validSheets))
            {
                if (File.Exists(model.FilePath))
                {
                    try
                    {
                        using PdfReader reader = new PdfReader(model.FilePath);
                        int pageCount = reader.NumberOfPages;
                        Log.Debug($"{model.SheetName}: {pageCount} pages");

                        for (int num = 1; num <= pageCount; num++)
                        {
                            PdfImportedPage page = copy.GetImportedPage(reader, num);
                            if (page != null && outputDocument.IsOpen())
                            {
                                copy.AddPage(page);
                                totalPages++;
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.Error(ex, $"{model.SheetName} failed");
                    }
                    finally
                    {
                        model.Dispose();
                        if (deleteOriginals)
                        {
                            RevitPathHelper.DeleteExistsFile(model.FilePath);
                        }
                    }
                }
                else
                {
                    Log.Warning($"{model.FilePath} not found");
                }
            }

            if (totalPages > 0)
            {
                Log.Information($"{totalPages} pages combined");
            }
            else
            {
                stream.Close();
                Log.Warning("No pages added");
                if (File.Exists(outputFullName))
                {
                    File.Delete(outputFullName);
                }
            }
        }
    }



}