using iTextSharp.text.pdf;
using RevitBIMTool.Models;
using RevitBIMTool.Utils.Common;
using Serilog;
using System.IO;
using Document = iTextSharp.text.Document;

namespace RevitBIMTool.Utils.ExportPdfUtil;

internal static class MergeHandlerOLD
{
    public static void CombinePDFsFromFolder(List<SheetModel> sheetModels, string directory, string outputFullName, bool deleted = true)
    {
        PathHelper.DeleteExistsFile(outputFullName);

        using Document outputDocument = new();
        using FileStream stream = new(outputFullName, FileMode.Create);
        using PdfCopy copy = new PdfSmartCopy(outputDocument, stream);

        outputDocument.Open();

        foreach (SheetModel model in SheetHelper.SortSheetModels(sheetModels))
        {
            if (File.Exists(model.TempFilePath))
            {
                PdfReader reader = new(model.TempFilePath);
                reader.ConsolidateNamedDestinations();
                Log.Debug(model.SheetName);
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
                        File.Delete(model.TempFilePath);
                    }
                }
            }
        }

        copy.Close();
        outputDocument.Close();
    }
}
