using Autodesk.Revit.DB;
using Microsoft.Win32;
using Serilog;


namespace RevitBIMTool.Utils.ExportPdfUtil;
internal static class RegistryHelper
{
    private const string registryPDFCreatorPath = "SOFTWARE\\pdfforge\\PDFCreator\\Settings\\ConversionProfiles\\0";

    private static void SetRegistryValue(RegistryKey regRoot, string regPath, string keyName, string value)
    {
        lock (Registry.LocalMachine)
        {
            try
            {
                using RegistryKey registryKey = regRoot.OpenSubKey(regPath, true);

                if (registryKey is not null)
                {
                    if (int.TryParse(value, out int intValue))
                    {
                        registryKey.SetValue(keyName, intValue, RegistryValueKind.DWord);
                    }
                    else if (!string.IsNullOrWhiteSpace(value))
                    {
                        registryKey.SetValue(keyName, value, RegistryValueKind.String);
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, ex.Message);
            }
        }
    }


    public static void ActivateSettingsForPDFCreator(string outputFolder)
    {
        string outputDirDoubleSlashes = outputFolder.Replace("\\", "\\\\");
        SetRegistryValue(Registry.CurrentUser, registryPDFCreatorPath + "\\AutoSave", "Enabled", "True");
        SetRegistryValue(Registry.CurrentUser, registryPDFCreatorPath + "\\OpenViewer", "Enabled", "False");
        SetRegistryValue(Registry.CurrentUser, registryPDFCreatorPath + "\\OpenViewer", "OpenWithPdfArchitect", "False");
        SetRegistryValue(Registry.CurrentUser, registryPDFCreatorPath, "TargetDirectory", outputDirDoubleSlashes);
        SetRegistryValue(Registry.CurrentUser, registryPDFCreatorPath, "FileNameTemplate", "<InputFilename>");
    }


    public static void SetOrientationForPdfCreator(PageOrientationType orientType)
    {
        string defaultValue = "Automatic";
        string keyTitle = "PageOrientation";
        string orientationText = Enum.GetName(typeof(PageOrientationType), orientType);
        string keyName = @"HKEY_CURRENT_USER\Software\pdfforge\PDFCreator\Settings\ConversionProfiles\0\PdfSettings";

        try
        {
            object check = Registry.GetValue(keyName, keyTitle, defaultValue);

            if (check is null)
            {
                throw new Exception("Value not found");
            }
            else if (check is string valueString)
            {
                if (valueString != orientationText)
                {
                    if (orientationText.Contains("Auto"))
                    {
                        orientationText = defaultValue;
                    }

                    Registry.SetValue(keyName, keyTitle, orientationText);
                }
            }
        }
        catch (Exception ex)
        {
            throw new Exception(ex.Message);
        }

    }



}
