using Microsoft.Win32;
using Serilog;
using System.Runtime.InteropServices;


namespace RevitBIMTool.Utils.SystemHelpers;
internal static class RegistryHelper
{
    private static readonly uint WM_SETTINGCHANGE = 26;
    private static readonly IntPtr HWND_BROADCAST = new(0xFFFF);


    public static bool IsKeyExists(RegistryKey rootKey, string path)
    {
        using RegistryKey registryKey = rootKey.OpenSubKey(path);
        return registryKey != null;
    }


    public static bool IsParameterExists(RegistryKey rootKey, string path, string name)
    {
        using RegistryKey registryKey = rootKey.OpenSubKey(path);
        return registryKey?.GetValue(name) != null;
    }


    public static object GetValue(RegistryKey rootKey, string path, string name)
    {
        try
        {
            using RegistryKey regKey = rootKey.OpenSubKey(path);

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


    public static void SetValue(RegistryKey rootKey, string path, string name, object value)
    {
        lock (rootKey)
        {
            try
            {
                int retryCount = 0;

                while (retryCount < 10)
                {
                    retryCount++;

                    Thread.Sleep(100);

                    if (IsParameterExists(rootKey, path, name))
                    {
                        using RegistryKey regKey = rootKey.OpenSubKey(path, true);

                        if (regKey is not null)
                        {
                            if (value is int intValue)
                            {
                                regKey.SetValue(name, intValue, RegistryValueKind.DWord);
                            }
                            else if (value is string strValue)
                            {
                                regKey.SetValue(name, strValue, RegistryValueKind.String);
                            }

                            regKey.Flush();

                            return;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, $"Failed to set registry value: {name}, {ex.Message}");
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


    public static bool CreateValue(RegistryKey rootKey, string path, string name, object value)
    {
        lock (rootKey)
        {
            try
            {
                using RegistryKey regKey = rootKey.CreateSubKey(path);

                if (regKey is not null)
                {
                    object currentValue = regKey.GetValue(name);

                    if (currentValue is null)
                    {
                        if (value is int intValue)
                        {
                            regKey.SetValue(name, intValue, RegistryValueKind.DWord);
                        }
                        else if (value is string strValue)
                        {
                            regKey.SetValue(name, strValue, RegistryValueKind.String);
                        }
                    }

                    regKey.Flush();

                    return ApplyRegistryChanges();
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to create registry parameter {path}: {ex.Message}");
            }

            return false;
        }
    }


    [DllImport("user32.DLL")]
    public static extern bool SendNotifyMessageA(IntPtr hWnd, uint msg, int wParam, int lParam);


    private static bool ApplyRegistryChanges()
    {
        return SendNotifyMessageA(HWND_BROADCAST, WM_SETTINGCHANGE, 0, 0);
    }

}
