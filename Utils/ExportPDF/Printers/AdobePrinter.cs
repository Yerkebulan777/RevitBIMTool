using Autodesk.Revit.DB;
using Microsoft.Win32;
using RevitBIMTool.Model;
using RevitBIMTool.Utils.SystemHelpers;


namespace RevitBIMTool.Utils.ExportPDF.Printers
{
    internal sealed class AdobePDFPrinter : PrinterControl
    {
        public override string RegistryPath => @"SOFTWARE\Adobe\Acrobat Distiller\Printer";
        public override string PrinterName => "Adobe PDF";


        public override void InitializePrinter()
        {
            string autoSave = System.IO.Path.Combine(RegistryPath, "AutoSave");
            string outputDirKey = System.IO.Path.Combine(RegistryPath, "OutputDir");
            string promptUserKey = System.IO.Path.Combine(RegistryPath, "PromptForAdobePDF");

            // Включаем автосохранение
            RegistryHelper.SetValue(Registry.CurrentUser, autoSave, "Enabled", "True");

            // Устанавливаем каталог для сохранения файлов
            RegistryHelper.SetValue(Registry.CurrentUser, outputDirKey, "Directory", "<InputFilePath>");

            // Отключаем запрос пользователя на ввод имени файла
            RegistryHelper.SetValue(Registry.CurrentUser, promptUserKey, "Enabled", "False");

            // Настраиваем шаблон имени файла
            RegistryHelper.SetValue(Registry.CurrentUser, RegistryPath, "FileNameTemplate", "<InputFilename>");

            // Отключаем уведомления
            RegistryHelper.SetValue(Registry.CurrentUser, RegistryPath, "ShowAllNotifications", "False");

            // Пропускаем диалог печати
            RegistryHelper.SetValue(Registry.CurrentUser, RegistryPath, "SkipPrintDialog", "True");

            // Устанавливаем статус принтера
            RegistryHelper.SetValue(Registry.CurrentUser, PrintHandler.StatusPath, PrinterName, 1);
        }


        public override void ResetPrinterSettings()
        {
            string desktop = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);

            // Сбрасываем настройки сохранения на рабочий стол
            RegistryHelper.SetValue(Registry.CurrentUser, RegistryPath, "OutputDir", desktop);

            // Сбрасываем шаблон имени файла
            RegistryHelper.SetValue(Registry.CurrentUser, RegistryPath, "FileNameTemplate", "<Title>");

            // Отключаем принтер
            RegistryHelper.SetValue(Registry.CurrentUser, PrintHandler.StatusPath, PrinterName, 0);
        }


        public override bool Print(Document doc, string folder, SheetModel model)
        {
            // Настраиваем директорию для сохранения
            RegistryHelper.SetValue(Registry.CurrentUser, RegistryPath, "OutputDir", folder.Replace("\\", "\\\\"));
            return PrintHandler.PrintSheet(doc, folder, model);
        }
    }

}
