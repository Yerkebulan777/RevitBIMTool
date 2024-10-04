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


    public static void SetValue(RegistryKey root, string path, string name, object value)
    {
        lock (Registry.LocalMachine)
        {
            try
            {
                using RegistryKey registryKey = root.OpenSubKey(path, true);

                if (value is int intValue)
                {
                    registryKey.SetValue(name, intValue, RegistryValueKind.DWord);
                }
                else if (value is string stringValue)
                {
                    registryKey.SetValue(name, stringValue, RegistryValueKind.String);
                }
                else
                {
                    throw new ArgumentException($"RegistryKey {name} {registryKey}");
                }

                registryKey.Flush();
            }
            catch (Exception ex)
            {
                Log.Error($"Registry {path} parameter {name}");
                Log.Error(ex, $"Set value failed: {ex}");
            }
            finally
            {
                Log.Debug($"Set to {name} value {value}");

                if (ApplyRegistryChanges())
                {
                    Thread.Sleep(100);
                }
            }
        }
    }


    public static object CreateParameter(RegistryKey root, string path, string name, object defaultValue)
    {
        object value = GetValue(root, path, name);

        lock (Registry.LocalMachine)
        {
            if (value is null)
            {
                try
                {
                    using RegistryKey regKey = root.OpenSubKey(path, true);
                    regKey?.SetValue(name, defaultValue);
                    regKey?.Flush();
                }
                catch (Exception ex)
                {
                    Log.Error(ex, $"Create parameter in {path} {ex.Message}");
                }
                finally
                {
                    if (ApplyRegistryChanges())
                    {
                        Thread.Sleep(100);
                    }
                }
            }
        }

        return value;
    }


    [DllImport("user32.DLL")]
    public static extern bool SendNotifyMessageA(IntPtr hWnd, uint msg, int wParam, int lParam);


    private static bool ApplyRegistryChanges()
    {
        return SendNotifyMessageA(HWND_BROADCAST, WM_SETTINGCHANGE, 0, 0);
    }

}
