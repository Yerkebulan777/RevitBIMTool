using iTextSharp.text.exceptions;
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

        PathHelper.DeleteExistsFile(outputFullName);

        if (validSheets is null || !validSheets.Any())
        {
            Log.Warning("No valid sheets to merge");
        }

        using Document outputDocument = new();
        using FileStream stream = new(outputFullName, FileMode.Create);
        using PdfCopy copy = new(outputDocument, stream);
        outputDocument.Open();
        int totalPages = 0;

        Log.Information("Merging {SheetCount} sheets", validSheets?.Count ?? 0);

        foreach (SheetModel model in SheetHelper.SortSheetModels(validSheets))
        {
            Log.Debug($"Sheet: {model.SheetName}");

            if (!File.Exists(model.TempFilePath))
            {
                Log.Warning($"File not found: {model.TempFilePath}");
            }

            try
            {
                using PdfReader reader = new(model.TempFilePath);

                int pageCount = reader.NumberOfPages;

                for (int num = 1; num <= pageCount; num++)
                {
                    try
                    {
                        PdfImportedPage page = copy.GetImportedPage(reader, num);
                        copy.AddPage(page);
                        totalPages++;
                    }
                    catch (Exception ex)
                    {
                        Log.Error(ex, $"Error adding page {num}");
                    }
                }
            }
            catch (BadPasswordException ex)
            {
                Log.Error(ex, $"Encrypt error: {ex.Message}");
            }
            catch (Exception ex)
            {
                Log.Error(ex, $"Failed: {ex.Message}");
            }
            finally
            {
                model.Dispose();

                if (deleteOriginals)
                {
                    PathHelper.DeleteExistsFile(model.TempFilePath);
                }
            }

        }

        Log.Information("Merged {TotalPages} pages", totalPages);

        if (totalPages == 0 && File.Exists(outputFullName))
        {
            File.Delete(outputFullName);
        }



    }
}