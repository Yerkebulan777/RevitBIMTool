using Microsoft.Win32;
using Serilog;
using System.Runtime.InteropServices;


namespace RevitBIMTool.Utils.SystemHelpers;
internal static class RegistryHelper
{
    private const int maxRetries = 10;
    private static readonly uint WM_SETTINGCHANGE = 26;
    private static readonly IntPtr HWND_BROADCAST = new(0xFFFF);


    public static bool IsKeyExists(RegistryKey rootKey, string path)
    {
        using RegistryKey registryKey = rootKey.OpenSubKey(path);

        if (registryKey is null)
        {
            return false;
        }

        return true;
    }


    public static bool IsValueExists(RegistryKey rootKey, string path, string name)
    {
        using RegistryKey registryKey = rootKey.OpenSubKey(path);
        object value = registryKey?.GetValue(name);

        if (value is null)
        {
            return false;
        }

        return true;
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
            int retryCount = 0;

            while (retryCount < maxRetries)
            {
                Thread.Sleep(100); retryCount++;

                if (IsValueExists(rootKey, path, name))
                {
                    using RegistryKey regKey = rootKey.OpenSubKey(path, true);

                    if (regKey != null)
                    {
                        SetRegistryValue(regKey, name, value);
                        regKey.Flush();
                        return;
                    }
                }
            }

            throw new InvalidOperationException($"Failed to set registry value: {name}");

        }
    }


    private static void SetRegistryValue(RegistryKey regKey, string name, object value)
    {
        try
        {
            if (value is int intValue)
            {
                regKey.SetValue(name, intValue, RegistryValueKind.DWord);
            }
            else if (value is string strValue)
            {
                regKey.SetValue(name, strValue, RegistryValueKind.String);
            }
            else
            {
                throw new ArgumentException($"Unsupported type: {value.GetType()}");
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
