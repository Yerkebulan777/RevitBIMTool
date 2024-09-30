using Microsoft.Win32;
using Serilog;
using System.IO;
using System.Runtime.InteropServices;


namespace RevitBIMTool.Utils.ExportPdfUtil;
internal static class RegistryHelper
{
    private static readonly uint WM_SETTINGCHANGE = 26;
    private static readonly IntPtr HWND_BROADCAST = new IntPtr(0xFFFF);


    public static bool IsPathExists(string installPath)
    {
        using RegistryKey regKey = RegistryKey.OpenBaseKey(RegistryHive.CurrentUser, RegistryView.Registry64);
        using RegistryKey registryKey = regKey.OpenSubKey(installPath);
        return registryKey != null;
    }


    private static bool SetRegistryValue(RegistryKey root, string regPath, string keyName, object value)
    {
        lock (Registry.LocalMachine)
        {
            try
            {
                using RegistryKey registryKey = root.OpenSubKey(regPath, true);

                if (registryKey is not null)
                {
                    if (value is int intValue)
                    {
                        registryKey.SetValue(keyName, intValue, RegistryValueKind.DWord);
                    }
                    else if (value is string stringValue)
                    {
                        registryKey.SetValue(keyName, stringValue, RegistryValueKind.String);
                    }
                }

                registryKey.Flush();

                return ApplyRegistryChanges();
            }
            catch (Exception ex)
            {
                Log.Error(ex, ex.Message);
            }

            return false;
        }
    }


    public static string GetValue(RegistryKey regRoot, string address, string property)
    {
        string value = string.Empty;

        lock (Registry.LocalMachine)
        {
            try
            {
                using RegistryKey registryKey = regRoot.OpenSubKey(address, false);

                if (registryKey is not null)
                {
                    value = registryKey.GetValue(property).ToString();
                    registryKey.Flush();
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, ex.Message);
            }

            return value;
        }
    }


    public static void ActivateSettingsForAdobePdf(string outputFile, string appPath)
    {
        string directory = Path.GetDirectoryName(outputFile);
        //string appPath = "C:\\Program Files\\Autodesk\\Revit 2023\\Revit.exe";
        string registryPath = @"SOFTWARE\Adobe\Acrobat Distiller\PrinterJobControl";
        _ = SetRegistryValue(Registry.CurrentUser, registryPath, "LastPdfPortFolder - Revit.exe", directory);
        _ = SetRegistryValue(Registry.CurrentUser, registryPath, appPath, outputFile);
    }


    public static void ActivateSettingsForPdfCreator(string outputFile)
    {
        string registryKey = @"SOFTWARE\pdfforge\PDFCreator\Settings\ConversionProfiles\0";
        _ = SetRegistryValue(Registry.CurrentUser, registryKey + "\\AutoSave", "Enabled", "True");
        _ = SetRegistryValue(Registry.CurrentUser, registryKey + "\\OpenViewer", "Enabled", "False");
        _ = SetRegistryValue(Registry.CurrentUser, registryKey + "\\OpenViewer", "OpenWithPdfArchitect", "False");
        _ = SetRegistryValue(Registry.CurrentUser, registryKey, "FileNameTemplate", "<InputFilename>");
        _ = SetRegistryValue(Registry.CurrentUser, registryKey, "TargetDirectory", outputFile);
    }

    private static void ResetPrinterOutput()
    {
        string registryKey = @"SOFTWARE\Microsoft\PrintToPDF";
        _ = SetRegistryValue(Registry.CurrentUser, registryKey, "PromptForFilename", 1);
    }

    private static void SetDefaultPrinterOutput(string outputFile)
    {
        string registryKey = @"SOFTWARE\Microsoft\PrintToPDF";
        _ = SetRegistryValue(Registry.CurrentUser, registryKey, "OutputFile", outputFile);
        _ = SetRegistryValue(Registry.CurrentUser, registryKey, "PromptForFilename", 0);
    }


    public static void SetPDF24Output(string outputFile)
    {
        // Путь в реестре к настройкам PDF24
        string pdf24Key = @"HKEY_CURRENT_USER\Software\PDFPrint\PDF24";

        // Установка пути сохранения и включение тихого режима (без запроса имени файла)
        Registry.SetValue(pdf24Key, "OutputDir", System.IO.Path.GetDirectoryName(outputFile));
        Registry.SetValue(pdf24Key, "OutputFile", System.IO.Path.GetFileName(outputFile));
        Registry.SetValue(pdf24Key, "SilentMode", 1);
    }

    public static void ResetPDF24Settings()
    {
        // Возврат стандартных настроек (с запросом имени файла)
        string pdf24Key = @"HKEY_CURRENT_USER\Software\PDFPrint\PDF24";
        Registry.SetValue(pdf24Key, "SilentMode", 0); // Включить запрос имени файла
    }


    [DllImport("user32.DLL")]
    public static extern bool SendNotifyMessageA(IntPtr hWnd, uint msg, int wParam, int lParam);


    private static bool ApplyRegistryChanges()
    {
        return SendNotifyMessageA(HWND_BROADCAST, WM_SETTINGCHANGE, 0, 0);
    }

}
