using Microsoft.Win32;
using Serilog;
using System.Diagnostics;
using System.Runtime.InteropServices;


namespace RevitBIMTool.Utils.SystemHelpers;
internal static class RegistryHelper
{
    private const int maxRetries = 10;
    private static readonly uint WM_SETTINGCHANGE = 26;
    private static readonly IntPtr HWND_BROADCAST = new(0xFFFF);
    private static readonly object lockObject = new();


    public static bool IsKeyExists(RegistryKey rootKey, string path)
    {
        using RegistryKey registryKey = rootKey.OpenSubKey(path);

        if (registryKey is null)
        {
            Log.Error("Registry key does not exist: {Path}!", path);
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
            Log.Error("Registry value does not exist: {Path}, {Name}!", path, name);
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
            Log.Error(ex, "GetValue failed: {Message}", ex.Message);
        }
        finally
        {
            Debug.WriteLine($"GetValue: {path}, {name}");
        }

        return null;
    }


    public static void SetValue(RegistryKey rootKey, string path, string name, object value)
    {
        lock (lockObject)
        {
            int retryCount = 0;

            while (retryCount < maxRetries)
            {
                retryCount++;
                Thread.Sleep(100);

                if (!IsKeyExists(rootKey, path))
                {
                    Log.Error("Registry value: {0}!", value);
                    throw new InvalidOperationException("");
                }
                else if (IsValueExists(rootKey, path, name))
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


    private static void SetRegistryValue(RegistryKey registryKey, string name, object value)
    {
        try
        {
            if (value is int intValue)
            {
                registryKey.SetValue(name, intValue, RegistryValueKind.DWord);
            }
            else if (value is string strValue)
            {
                registryKey.SetValue(name, strValue, RegistryValueKind.String);
            }
            else
            {
                throw new ArgumentException($"Unsupported type: {value.GetType()}");
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to set registry value: {0}, {1}", name, ex.Message);
        }
        finally
        {
            if (ApplyRegistryChanges(name))
            {
                Thread.Sleep(100);
            }
        }
    }


    private static bool ApplyRegistryChanges(string name)
    {
        Debug.WriteLine($"Registry value set: {name}");
        return SendNotifyMessageA(HWND_BROADCAST, WM_SETTINGCHANGE, 0, 0);
    }


    [DllImport("user32.DLL")]
    private static extern bool SendNotifyMessageA(IntPtr hWnd, uint msg, int wParam, int lParam);



}
