using Serilog;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;


namespace RevitBIMTool.Utils.SystemUtil;
internal static class SystemFolderOpener
{
    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);


    public static void CloseDirectory(string inputPath)
    {
        string inputName = Path.GetFileName(inputPath);

        Log.Debug($"Input folder name: ({inputName})");

        foreach (Process proc in Process.GetProcessesByName("explorer"))
        {
            if (inputName.EndsWith(proc.MainWindowTitle, StringComparison.OrdinalIgnoreCase))
            {
                Log.Debug($"Process {proc.MainWindowTitle} will be close");

                proc?.Kill();
                proc?.Dispose();
            }
        }
    }


    public static void OpenFolder(string directoryPath)
    {
        Log.Debug($"Start method {nameof(OpenFolder)}");

        if (Directory.Exists(directoryPath))
        {
            CloseDirectory(directoryPath);

            Process proc = Process.Start("explorer.exe", directoryPath);

            if (proc.WaitForExit(1000))
            {
                Log.Debug($"Opened folder ({directoryPath})");
            }

        }

    }


}

