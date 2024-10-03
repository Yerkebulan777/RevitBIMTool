using Microsoft.Win32;
using Serilog;
using System.IO;
using System.Runtime.InteropServices;


namespace RevitBIMTool.Utils.SystemHelpers;
internal static class RegistryHelper
{
    private static readonly uint WM_SETTINGCHANGE = 26;
    private static readonly IntPtr HWND_BROADCAST = new(0xFFFF);
    private const string registryPrintPath = @"SYSTEM\CurrentControlSet\Control\Print\Printers";


    public static bool IsRegistryKeyExists(RegistryHive hiveKey, string installPath)
    {
        using RegistryKey regKey = RegistryKey.OpenBaseKey(hiveKey, RegistryView.Default);
        using RegistryKey registryKey = regKey.OpenSubKey(installPath);
        return registryKey != null;
    }


    private static bool IsPrinterRegistry(string printerName)
    {
        const string registryPath = @"SYSTEM\CurrentControlSet\Control\Print\Printers";

        using RegistryKey key = Registry.LocalMachine.OpenSubKey(registryPath);
        return key?.GetSubKeyNames().Contains(printerName) ?? false;
    }


    public static object GetValue(RegistryKey root, string path, string name)
    {
        try
        {
            using RegistryKey regKey = root.OpenSubKey(path);

            if (regKey is not null)
            {
                object value = regKey.GetValue(name);
                regKey.Flush();
                return value;
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, $"GetValue failed: {ex.Message}");
        }

        return null;
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
                Log.Error(ex, $"Set status failed: {ex.Message}");
            }
            finally
            {
                _ = ApplyRegistryChanges();
            }
        }
    }


    public static void CreateParameter(RegistryKey root, string path, string name, string defaultValue)
    {
        string value = (string)GetValue(root, path, name);

        if (string.IsNullOrEmpty(value))
        {
            lock (Registry.LocalMachine)
            {
                try
                {
                    using RegistryKey regKey = root.OpenSubKey(path, true);
                    using RegistryKey key = regKey.CreateSubKey(name);
                    key?.SetValue(name, defaultValue);
                }
                catch (Exception ex)
                {
                    Log.Error(ex, $"Create parameter failed: {ex.Message}");
                }
                finally
                {
                    _ = ApplyRegistryChanges();
                }
            }
        }

    }


    private static int GetPrinterStatus(string printerName, out string output)
    {
        string registryPath = Path.Combine(registryPrintPath, printerName);

        object status = GetValue(Registry.CurrentUser, registryPath, "Status");

        return GetStatusDescription((int)status, out output);
    }


    private static int GetStatusDescription(int status, out string description)
    {
        description = status switch
        {
            0 => $"Idle (0)",
            1 => $"Paused (1)",
            2 => $"Error (2)",
            3 => $"Pending Deletion (3)",
            4 => $"Paper Jam (4)",
            5 => $"Paper Out (5)",
            6 => $"Manual Feed (6)",
            7 => $"Offline (7)",
            16 => $"Printing (16)",
            32 => $"Waiting (32)",
            64 => $"Processing (64)",
            128 => $"Initializing (128)",
            _ => $"Unknown Status",
        };
        return status;
    }


    [DllImport("user32.DLL")]
    public static extern bool SendNotifyMessageA(IntPtr hWnd, uint msg, int wParam, int lParam);


    private static bool ApplyRegistryChanges()
    {
        return SendNotifyMessageA(HWND_BROADCAST, WM_SETTINGCHANGE, 0, 0);
    }


}
