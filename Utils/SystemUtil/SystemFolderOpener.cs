using Serilog;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;


namespace RevitBIMTool.Utils.SystemUtil;
internal static class SystemFolderOpener
{
    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);


    public static bool IsExplorerWindowOpenForFolder(string folderPath, out IntPtr handle)
    {
        handle = IntPtr.Zero;

        foreach (Process proc in Process.GetProcessesByName("explorer"))
        {
            string path = Path.GetFullPath(proc.MainModule.FileName);

            if (path.Equals(folderPath, StringComparison.OrdinalIgnoreCase))
            {
                handle = proc.MainWindowHandle;
                return true;
            }
        }

        return false;
    }


    public static void OpenFolderInExplorerIfNeeded(string directoryPath)
    {
        Log.Debug($"Start method {nameof(OpenFolderInExplorerIfNeeded)}");

        if (!Directory.Exists(directoryPath))
        {
            Log.Debug($"Folder not found: {directoryPath}");
            return;
        }

        if (IsExplorerWindowOpenForFolder(directoryPath, out IntPtr handle))
        {
            Log.Debug($"Window for folder ({directoryPath}) found");
            // Try to bring the window to the foreground
            if (SetForegroundWindow(handle))
            {
                Log.Debug($"Folder brought to the foreground");
                return;
            }
        }

        Process proc = Process.Start("explorer.exe", directoryPath);

        if (!proc.HasExited)
        {
            Log.Debug($"Opened folder ({directoryPath})");
        }
    }



}

