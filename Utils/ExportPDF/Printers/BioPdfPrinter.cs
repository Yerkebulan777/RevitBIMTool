using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Drawing.Printing;
using Microsoft.Win32;
using RevitBIMTool.Models;
using RevitBIMTool.Utils.ExportPDF;
using RevitBIMTool.Utils.SystemHelpers;
using Serilog;
using Autodesk.Revit.DB;

namespace RevitBIMTool.Utils.ExportPDF.Printers;

internal sealed class BioPdfPrinter : PrinterControl
{
    public override string RegistryPath => @"PDF Writer\PDF Writer - bioPDF";
    public override string PrinterName => "PDF Writer - bioPDF";
    public override bool IsInternalPrinter => false;

    public override void InitializePrinter()
    {
        PrinterStateManager.ReservePrinter(PrinterName);

        string settingsPath = Path.Combine(RegistryPath, "settings.ini");

        // Основные настройки для автоматического создания PDF
        RegistryHelper.SetValue(Registry.CurrentUser, settingsPath, "DisableOptionDialog", "yes");
        RegistryHelper.SetValue(Registry.CurrentUser, settingsPath, "ShowSettings", "never");
        RegistryHelper.SetValue(Registry.CurrentUser, settingsPath, "ShowSaveAS", "never");
        RegistryHelper.SetValue(Registry.CurrentUser, settingsPath, "ShowProgress", "yes");
        RegistryHelper.SetValue(Registry.CurrentUser, settingsPath, "ShowPDF", "never");
        RegistryHelper.SetValue(Registry.CurrentUser, settingsPath, "ShowProgressFinished", "no");
        RegistryHelper.SetValue(Registry.CurrentUser, settingsPath, "ConfirmOverwrite", "no");

        // Настройки качества документа
        RegistryHelper.SetValue(Registry.CurrentUser, settingsPath, "Target", "printer");

        // Дополнительные метаданные
        RegistryHelper.SetValue(Registry.CurrentUser, settingsPath, "Subject", "PDF Printer Export");
        RegistryHelper.SetValue(Registry.CurrentUser, settingsPath, "Creator", "bioPDF Automation");

        Log.Debug("bioPDF printer initialized with automatic PDF creation settings");
    }

    public override void ReleasePrinterSettings()
    {
        string settingsPath = Path.Combine(RegistryPath, "settings.ini");

        // Возвращаем настройки по умолчанию
        RegistryHelper.SetValue(Registry.CurrentUser, settingsPath, "DisableOptionDialog", "no");
        RegistryHelper.SetValue(Registry.CurrentUser, settingsPath, "ShowSettings", "always");
        RegistryHelper.SetValue(Registry.CurrentUser, settingsPath, "ShowSaveAS", "nofile");
        RegistryHelper.SetValue(Registry.CurrentUser, settingsPath, "ShowPDF", "yes");
        RegistryHelper.SetValue(Registry.CurrentUser, settingsPath, "ShowProgressFinished", "yes");
        RegistryHelper.SetValue(Registry.CurrentUser, settingsPath, "ConfirmOverwrite", "yes");

        PrinterStateManager.ReleasePrinter(PrinterName);
        Log.Debug("bioPDF printer settings restored to interactive mode");
    }


    public override bool DoPrint(Document doc, SheetModel model, string folder)
    {
        string settingsPath = Path.Combine(RegistryPath, "settings.ini");

        // Устанавливаем путь и имя файла для сохранения
        string outputPath = Path.Combine(folder, model.SheetName);
        RegistryHelper.SetValue(Registry.CurrentUser, settingsPath, "Output", outputPath);

        // Устанавливаем метаданные документа
        RegistryHelper.SetValue(Registry.CurrentUser, settingsPath, "Title", model.SheetName);
        RegistryHelper.SetValue(Registry.CurrentUser, settingsPath, "Author", Environment.UserName);

        return PrintHelper.ExecutePrint(doc, model, folder);
    }



}