using iTextSharp.text.pdf;
using RevitBIMTool.Model;
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

        Log.Information($"Sheets count for PDF combining: {validSheets?.Count ?? 0}");

        RevitPathHelper.DeleteExistsFile(outputFullName);

        if (validSheets is not null && validSheets.Any())
        {
            using FileStream stream = new(outputFullName, FileMode.Create);

            using Document outputDocument = new();

            using PdfCopy copy = new PdfSmartCopy(outputDocument, stream);

            outputDocument.Open();

            int totalPages = 0;

            foreach (SheetModel model in SheetModel.SortSheetModels(validSheets))
            {
                PdfReader reader = null;

                try
                {
                    reader = new PdfReader(model.FilePath);
                    reader.ConsolidateNamedDestinations();

                    for (int num = 1; num <= reader.NumberOfPages; num++)
                    {
                        PdfImportedPage page = copy.GetImportedPage(reader, num);
                        if (page != null && outputDocument.IsOpen())
                        {
                            copy.AddPage(page);
                            totalPages++;
                        }
                    }

                    copy.FreeReader(reader);
                }
                catch (Exception ex)
                {
                    Log.Error(ex, $"Error processing PDF sheet {model.SheetName}: {ex.Message}");
                }
                finally
                {
                    reader?.Close();
                    model.Dispose();

                    if (deleteOriginals)
                    {
                        RevitPathHelper.DeleteExistsFile(model.FilePath);
                    }
                }
            }

            Log.Information($"Successfully combined {totalPages} total pages");
        }
    }




}