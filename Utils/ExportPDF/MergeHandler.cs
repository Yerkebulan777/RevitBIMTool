using iTextSharp.text.pdf;
using RevitBIMTool.Model;
using RevitBIMTool.Utils.Common;
using Serilog;
using System.IO;
using Document = iTextSharp.text.Document;

namespace RevitBIMTool.Utils.ExportPDF;

internal static class MergeHandler
{
    public static void Combine(List<SheetModel> sheetModels, string outputFullName, bool deleted = true)
    {
        List<SheetModel> validSheets = sheetModels?.Where(s => s.IsSuccessfully).ToList();

        RevitPathHelper.DeleteExistsFile(outputFullName);

        if (validSheets is null || validSheets.Count == 0)
        {
            Log.Error("Нет листов для объединения!");
            return;
        }

        using FileStream stream = new(outputFullName, FileMode.Create);
        using Document outputDocument = new();
        using PdfCopy copy = new PdfSmartCopy(outputDocument, stream);
        outputDocument.Open();

        foreach (SheetModel model in SheetModel.SortSheetModels(validSheets))
        {
            PdfReader reader = null;

            if (File.Exists(model.FilePath))
            {
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
                        }
                    }
                }
                catch (Exception ex)
                {
                    Log.Error(ex, ex.Message);
                }
                finally
                {
                    model.Dispose();
                    if (reader != null)
                    {
                        copy.FreeReader(reader);
                        reader.Close();
                        if (deleted)
                        {
                            Log.Debug(model.SheetName);
                            RevitPathHelper.DeleteExistsFile(model.FilePath);
                        }
                    }
                }
            }
        }
    }



}