using Microsoft.Win32;
using Serilog;
using System.IO;
using System.Runtime.InteropServices;


namespace RevitBIMTool.Utils.ExportPdfUtil;
internal static class RegistryHelper
{
    private static readonly uint WM_SETTINGCHANGE = 26;
    private static readonly IntPtr HWND_BROADCAST = new(0xFFFF);


    public static bool IsRegistryKeyExists(string installPath)
    {
        using RegistryKey regKey = RegistryKey.OpenBaseKey(RegistryHive.CurrentUser, RegistryView.Registry64);
        using RegistryKey registryKey = regKey.OpenSubKey(installPath);
        return registryKey != null;
    }


    public static void SetValue(RegistryKey root, string regPath, string keyName, object value)
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
            }
            catch (Exception ex)
            {
                Log.Error(ex, $"SetValue failed: {ex.Message}");
            }
            finally
            {
                _ = ApplyRegistryChanges();
            }
        }
    }


    public static string GetValue(RegistryKey root, string path, string name)
    {
        string value = null;

        try
        {
            using RegistryKey registryKey = root.OpenSubKey(path);

            if (registryKey is not null)
            {
                value = registryKey.GetValue(name).ToString();
                registryKey.Flush();
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, $"GetValue failed: {ex.Message}");
        }

        return value;
    }


    public static void CreateParameter(RegistryKey root, string path, string name, string defaultValue)
    {
        string value = GetValue(root, path, name);

        if (string.IsNullOrEmpty(value))
        {
            try
            {
                using RegistryKey registryKey = root.OpenSubKey(path, true);
                using RegistryKey key = registryKey.CreateSubKey(name);
                key?.SetValue(name, defaultValue);
            }
            catch (Exception ex)
            {
                Log.Error(ex, $"CreateParameter failed: {ex.Message}");
            }
            finally
            {
                _ = ApplyRegistryChanges();
            }
        }

    }


    public static void ActivateSettingsForAdobePdf(string outputFile, string appPath)
    {
        string directory = Path.GetDirectoryName(outputFile);
        //string appPath = "C:\\Program Files\\Autodesk\\Revit 2023\\Revit.exe";
        string registryPath = @"SOFTWARE\Adobe\Acrobat Distiller\PrinterJobControl";
        SetValue(Registry.CurrentUser, registryPath, "LastPdfPortFolder - Revit.exe", directory);
        SetValue(Registry.CurrentUser, registryPath, appPath, outputFile);
    }


    public static void ActivateSettingsForPdfCreator(string outputFile)
    {
        string registryKey = @"SOFTWARE\pdfforge\PDFCreator\Settings\ConversionProfiles\0";
        SetValue(Registry.CurrentUser, registryKey + "\\AutoSave", "Enabled", "True");
        SetValue(Registry.CurrentUser, registryKey + "\\OpenViewer", "Enabled", "False");
        SetValue(Registry.CurrentUser, registryKey + "\\OpenViewer", "OpenWithPdfArchitect", "False");
        SetValue(Registry.CurrentUser, registryKey, "FileNameTemplate", "<InputFilename>");

    }


    private static void ResetPrinterOutput()
    {
        string registryKey = @"SOFTWARE\Microsoft\PrintToPDF";
        SetValue(Registry.CurrentUser, registryKey, "PromptForFilename", 1);
    }


    private static void SetDefaultPrinterOutput(string outputFile)
    {
        string registryKey = @"SOFTWARE\Microsoft\PrintToPDF";
        SetValue(Registry.CurrentUser, registryKey, "OutputFile", outputFile);
        SetValue(Registry.CurrentUser, registryKey, "PromptForFilename", 0);
    }


    [DllImport("user32.DLL")]
    public static extern bool SendNotifyMessageA(IntPtr hWnd, uint msg, int wParam, int lParam);


    private static bool ApplyRegistryChanges()
    {
        return SendNotifyMessageA(HWND_BROADCAST, WM_SETTINGCHANGE, 0, 0);
    }

}
