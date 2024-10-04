using Microsoft.Win32;
using Serilog;
using System.Runtime.InteropServices;


namespace RevitBIMTool.Utils.SystemHelpers;
internal static class RegistryHelper
{
    private static readonly uint WM_SETTINGCHANGE = 26;
    private static readonly IntPtr HWND_BROADCAST = new(0xFFFF);

    private static RegistryHive GetRegistryHive(RegistryKey root)
    {
        return root.Name switch
        {
            "HKEY_USERS" => RegistryHive.Users,
            "HKEY_CURRENT_USER" => RegistryHive.CurrentUser,
            "HKEY_CLASSES_ROOT" => RegistryHive.ClassesRoot,
            "HKEY_CURRENT_CONFIG" => RegistryHive.CurrentConfig,
            _ => RegistryHive.LocalMachine
        };
    }


    public static bool IsSubKeyExists(RegistryKey root, string registryPath)
    {
        try
        {
            RegistryHive registryHive = GetRegistryHive(root);
            using RegistryKey regKey = RegistryKey.OpenBaseKey(registryHive, RegistryView.Default);
            using RegistryKey registryKey = regKey.OpenSubKey(registryPath);
            return registryKey != null;
        }
        catch (Exception ex)
        {
            Log.Error(ex, ex.Message);
            return false;
        }
    }


    public static object GetValue(RegistryKey root, string path, string name)
    {
        try
        {
            using RegistryKey regKey = root.OpenSubKey(path);

            if (regKey is not null)
            {
                return regKey.GetValue(name);
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, $"GetValue failed: {ex.Message}");
        }

        return null;
    }


    public static object SetValue(RegistryKey root, string path, string name, object value)
    {
        object result = null;

        try
        {
            using RegistryKey key = root.OpenSubKey(path, true) ?? root.CreateSubKey(path);

            result = key.GetValue(name);

            if (result is null || result != value)
            {
                key.SetValue(name, value);
                key.Flush();
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, $"SetValue parameter in {path} {ex.Message}");
        }
        finally
        {
            if (ApplyRegistryChanges())
            {
                Thread.Sleep(100);
            }
        }

        return result;
    }


    [DllImport("user32.DLL")]
    public static extern bool SendNotifyMessageA(IntPtr hWnd, uint msg, int wParam, int lParam);


    private static bool ApplyRegistryChanges()
    {
        return SendNotifyMessageA(HWND_BROADCAST, WM_SETTINGCHANGE, 0, 0);
    }

}
