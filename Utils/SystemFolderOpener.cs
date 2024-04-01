using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;


namespace RevitBIMTool.Utils;
internal static class SystemFolderOpener
{
    [DllImport("user32.dll")]
    private static extern IntPtr FindWindow(string lpClassName, string lpWindowName);

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);


    public static void OpenFolder(string directoryPath)
    {
        if (Directory.Exists(directoryPath))
        {
            try
            {
                string folderName = Path.GetFileName(directoryPath);

                foreach (Process proc in Process.GetProcessesByName("explorer"))
                {
                    if (proc.MainWindowTitle.Contains(folderName))
                    {
                        if (SetForegroundWindow(proc.MainWindowHandle))
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
