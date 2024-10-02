using Serilog;
using System.Diagnostics;


namespace RevitBIMTool.Utils
{
    public static class FileUnlockHelper
    {
        public static bool TryUnlockFile(string filePath)
        {
            try
            {
                Process handleProcess = new();
                handleProcess.StartInfo.FileName = "handle.exe";
                handleProcess.StartInfo.Arguments = $"-a \"{filePath}\"";
                handleProcess.StartInfo.RedirectStandardOutput = true;
                handleProcess.StartInfo.UseShellExecute = false;
                handleProcess.StartInfo.CreateNoWindow = true;

                if (handleProcess.Start())
                {
                    string output = handleProcess.StandardOutput.ReadToEnd();

                    handleProcess.WaitForExit();

                    int pid = ParseHandleOutput(output);

                    if (pid > 0)
                    {
                        try
                        {
                            Process.GetProcessById(pid).Kill();
                        }
                        catch (Exception ex)
                        {
                            Log.Error($"Ошибка при завершении процесса: {ex.Message}");
                            return false;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error($"Ошибка при разблокировке файла: {ex.Message}");
                return false;
            }

            return true;
        }


        private static int ParseHandleOutput(string output)
        {
            try
            {
                string[] lines = output.Split(new[] { Environment.NewLine }, StringSplitOptions.None);

                foreach (string line in lines)
                {
                    if (line.Contains("pid:"))
                    {
                        string pidString = line.Substring(line.IndexOf("pid:") + 4).Trim();

                        if (int.TryParse(pidString, out int pid))
                        {
                            return pid;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Ошибка при парсинге вывода handle.exe: {ex.Message}");
            }

            return -1;
        }

    }
}
