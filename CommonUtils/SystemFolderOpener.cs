using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;


namespace CommonUtils
{
    internal static class SystemFolderOpener
    {
        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);


        public static void CloseDirectory(string inputPath)
        {
            string inputName = Path.GetFileName(inputPath);

            StringComparison comparison = StringComparison.OrdinalIgnoreCase;

            foreach (Process proc in Process.GetProcessesByName("explorer"))
            {
                if (inputName.EndsWith(proc.MainWindowTitle, comparison))
                {
                    proc?.Kill();
                    proc?.Dispose();
                }
            }
        }


        public static void OpenFolder(string directoryPath)
        {
            if (Directory.Exists(directoryPath))
            {
                CloseDirectory(directoryPath);

                Process proc = Process.Start("explorer.exe", directoryPath);

                if (proc.WaitForExit(1000))
                {
                    Debug.WriteLine($"Opened: {directoryPath}");
                }
            }
        }


    }

}
