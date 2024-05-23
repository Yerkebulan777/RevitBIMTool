using SHDocVw;
using Shell32;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;


namespace RevitBIMTool.Utils.SystemUtil;
internal static class SystemFolderOpener
{
    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);


    public static void OpenFolder(string directoryPath)
    {
        if (Directory.Exists(directoryPath))
        {
            try
            {
                Shell shell = new();
                ShellWindows shellWindows = shell.Windows();

                foreach (InternetExplorer window in shellWindows)
                {
                    string path = Path.GetFullPath(window.FullName).ToLower();

                    if (path == directoryPath.ToLower())
                    {
                        if (SetForegroundWindow((IntPtr)window.HWND))
                        {
                            return;
                        }
                    }
                }

                _ = Process.Start("explorer.exe", directoryPath);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Произошла ошибка: {ex.Message}");
            }
        }
    }


}

