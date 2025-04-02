using Autodesk.Revit.DB;
using Microsoft.Win32;
using RevitBIMTool.Models;
using Serilog;

namespace RevitBIMTool.Utils.ExportPDF.Printers
{
    internal sealed class PDFillPrinter : PrinterControl
    {
        public override string RegistryPath => @"Software\PlotSoft\PDFill\PDFWriter";
        public override string PrinterName => "PDFill PDF Writer";
        public override bool IsInternalPrinter => false;


        public override void InitializePrinter()
        {
            try
            {
                PrinterStateManager.ReservePrinter(PrinterName);

                using (RegistryKey key = Registry.CurrentUser.CreateSubKey(RegistryPath))
                {
                    if (key != null)
                    {
                        // Настройка автоматического сохранения
                        key.SetValue("AutoSave", 1, RegistryValueKind.DWord);

                        // Отключение диалоговых окон
                        key.SetValue("ShowSaveDialog", 0, RegistryValueKind.DWord);
                        key.SetValue("ShowProgress", 0, RegistryValueKind.DWord);
                        key.SetValue("ShowResult", 0, RegistryValueKind.DWord);
                        key.SetValue("ShowSettings", 0, RegistryValueKind.DWord);

                        // Разрешение перезаписи файлов
                        key.SetValue("OverwriteFile", 1, RegistryValueKind.DWord);

                        // Отключение открытия PDF после создания
                        key.SetValue("OpenAfterCreation", 0, RegistryValueKind.DWord);
                    }
                }

                Log.Debug("PDFill PDF Writer инициализирован");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Ошибка инициализации PDFill PDF Writer: {Message}", ex.Message);
                throw new InvalidOperationException("Ошибка инициализации PDFill PDF Writer", ex);
            }
        }


        public override void ReleasePrinterSettings()
        {
            try
            {
                using RegistryKey key = Registry.CurrentUser.OpenSubKey(RegistryPath, true);
                if (key != null)
                {
                    key.SetValue("AutoSave", 0, RegistryValueKind.DWord);
                    key.SetValue("OpenAfterCreation", 1, RegistryValueKind.DWord);
                    key.SetValue("ShowSaveDialog", 1, RegistryValueKind.DWord);
                    key.SetValue("ShowProgress", 1, RegistryValueKind.DWord);
                    key.SetValue("ShowResult", 1, RegistryValueKind.DWord);
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Ошибка при восстановлении настроек PDFill: {Message}", ex.Message);
            }
            finally
            {
                PrinterStateManager.ReleasePrinter(PrinterName);
            }
        }


        public override bool DoPrint(Document doc, SheetModel model, string folder)
        {
            using (RegistryKey key = Registry.CurrentUser.CreateSubKey(RegistryPath))
            {
                if (key != null)
                {
                    // Настройка пути сохранения
                    key.SetValue("SavePath", folder, RegistryValueKind.String);
                    key.SetValue("Title", model.SheetName, RegistryValueKind.String);
                    key.SetValue("DefaultFileName", model.SheetName, RegistryValueKind.String);
                    key.SetValue("Subject", "Generated Document", RegistryValueKind.String);
                    key.SetValue("Author", Environment.UserName, RegistryValueKind.String);
                }
            }

            return PrintHelper.ExecutePrint(doc, model, folder);
        }
    }

}
