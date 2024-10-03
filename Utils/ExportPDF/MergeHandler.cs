using iTextSharp.text.pdf;
using RevitBIMTool.Model;
using Serilog;
using System.IO;
using Document = iTextSharp.text.Document;


namespace RevitBIMTool.Utils.ExportPDF;
internal static class MergeHandler
{
    public static void CombinePDFsFromFolder(List<SheetModel> sheetModels, string directory, string outputFullName, bool deleted = true)
    {
        RevitPathHelper.DeleteExistsFile(outputFullName);

        using FileStream stream = new(outputFullName, FileMode.Create);

        Document outputDocument = new();

        PdfCopy copy = new PdfSmartCopy(outputDocument, stream);

        outputDocument.Open();

        foreach (SheetModel model in SheetModel.SortSheetModels(sheetModels))
        {
            if (File.Exists(model.TempPath))
            {
                PdfReader reader = new(model.TempPath);

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
                        File.Delete(model.TempPath);
                    }
                }
            }
        }

        copy.Close();
        outputDocument.Close();
    }

}