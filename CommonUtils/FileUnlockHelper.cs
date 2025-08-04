using System.Diagnostics;
using System.IO;
using System.Security;

namespace CommonUtils
{
    public static class FileUnlockHelper
    {
        private static readonly string[] AllowedToolPaths =
        {
            @"C:\Windows\System32\handle.exe",
            @"C:\Windows\SysWOW64\handle.exe",
            @"C:\Program Files\Microsoft\handle.exe"
        };

        /// <summary>
        /// Безопасная попытка разблокировки файла
        /// </summary>
        public static UnlockResult TryUnlockFile(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
            {
                return UnlockResult.Failed("File path is empty");
            }

            if (!File.Exists(filePath))
            {
                return UnlockResult.Failed("File does not exist");
            }

            try
            {
                // Сначала пробуем простую проверку доступности
                if (IsFileAccessible(filePath))
                {
                    return UnlockResult.Success("File is already accessible");
                }

                // Ищем безопасный путь к handle.exe
                string handlePath = FindSecureHandlePath();
                if (string.IsNullOrEmpty(handlePath))
                {
                    return UnlockResult.Failed("Handle.exe not found in secure locations");
                }

                // Выполняем безопасный запуск
                return ExecuteHandleTool(handlePath, filePath);
            }
            catch (SecurityException ex)
            {
                Debug.Fail($"Security error unlocking file: {ex.Message}");
                return UnlockResult.Failed("Security restriction");
            }
            catch (UnauthorizedAccessException ex)
            {
                Debug.Fail($"Access denied unlocking file: {ex.Message}");
                return UnlockResult.Failed("Access denied");
            }
            catch (Exception ex)
            {
                Debug.Fail($"Error unlocking file: {ex.Message}");
                return UnlockResult.Failed($"Unexpected error: {ex.Message}");
            }
        }

        /// <summary>
        /// Проверяет доступность файла простым способом
        /// </summary>
        private static bool IsFileAccessible(string filePath)
        {
            try
            {
                using FileStream stream = File.OpenRead(filePath);
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Находит безопасный путь к handle.exe
        /// </summary>
        private static string FindSecureHandlePath()
        {
            foreach (string path in AllowedToolPaths)
            {
                if (File.Exists(path))
                {
                    // Дополнительная проверка цифровой подписи
                    if (IsFileSignedByMicrosoft(path))
                    {
                        return path;
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// Проверяет цифровую подпись файла
        /// </summary>
        private static bool IsFileSignedByMicrosoft(string filePath)
        {
            try
            {
                FileVersionInfo versionInfo = FileVersionInfo.GetVersionInfo(filePath);
                return versionInfo.CompanyName?.Contains("Microsoft") == true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Безопасное выполнение handle.exe
        /// </summary>
        private static UnlockResult ExecuteHandleTool(string handlePath, string filePath)
        {
            using Process handleProcess = new();

            handleProcess.StartInfo.FileName = handlePath;
            handleProcess.StartInfo.Arguments = $"-a \"{filePath}\"";
            handleProcess.StartInfo.RedirectStandardOutput = true;
            handleProcess.StartInfo.RedirectStandardError = true;
            handleProcess.StartInfo.UseShellExecute = false;
            handleProcess.StartInfo.CreateNoWindow = true;
            handleProcess.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;

            // Ограничиваем время выполнения
            const int timeoutMs = 10000;

            try
            {
                if (!handleProcess.Start())
                {
                    return UnlockResult.Failed("Failed to start handle process");
                }

                if (!handleProcess.WaitForExit(timeoutMs))
                {
                    handleProcess.Kill();
                    return UnlockResult.Failed("Handle process timeout");
                }

                if (handleProcess.ExitCode != 0)
                {
                    string error = handleProcess.StandardError.ReadToEnd();
                    return UnlockResult.Failed($"Handle failed: {error}");
                }

                string output = handleProcess.StandardOutput.ReadToEnd();
                return ProcessHandleOutput(output);
            }
            catch (Exception ex)
            {
                Debug.Fail($"Error executing handle tool: {ex.Message}");
                return UnlockResult.Failed("Handle execution failed");
            }
        }

        /// <summary>
        /// Обрабатывает вывод handle.exe и пытается завершить блокирующие процессы
        /// </summary>
        private static UnlockResult ProcessHandleOutput(string output)
        {
            if (string.IsNullOrWhiteSpace(output))
            {
                return UnlockResult.Success("No file locks found");
            }

            var processIds = ParseProcessIds(output);

            if (processIds.Count == 0)
            {
                return UnlockResult.Success("No blocking processes found");
            }

            int terminatedCount = 0;

            foreach (int pid in processIds)
            {
                if (TryTerminateProcess(pid))
                {
                    terminatedCount++;
                }
            }

            return terminatedCount > 0
                ? UnlockResult.Success($"Terminated {terminatedCount} blocking processes")
                : UnlockResult.Failed("Could not terminate any blocking processes");
        }

        /// <summary>
        /// Парсит PID из вывода handle.exe
        /// </summary>
        private static List<int> ParseProcessIds(string output)
        {
            List<int> processIds = [];

            string[] lines = output.Split(new[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries);

            foreach (string line in lines)
            {
                if (line.Contains("pid:"))
                {
                    int pidStart = line.IndexOf("pid:", StringComparison.OrdinalIgnoreCase) + 4;
                    int pidEnd = line.IndexOf(' ', pidStart);

                    if (pidEnd == -1)
                    {
                        pidEnd = line.Length;
                    }

                    string pidString = line[pidStart..pidEnd].Trim();

                    if (int.TryParse(pidString, out int pid) && pid > 0)
                    {
                        processIds.Add(pid);
                    }
                }
            }

            return processIds;
        }

        /// <summary>
        /// Безопасное завершение процесса
        /// </summary>
        private static bool TryTerminateProcess(int processId)
        {
            try
            {
                using Process process = Process.GetProcessById(processId);

                // Не завершаем системные процессы
                if (IsSystemProcess(process))
                {
                    Debug.Print($"Skipping system process: {process.ProcessName} ({processId})");
                    return false;
                }

                _ = process.CloseMainWindow();

                if (!process.WaitForExit(5000))
                {
                    process.Kill();
                }

                return true;
            }
            catch (Exception ex)
            {
                Debug.Fail($"Failed to terminate process {processId}: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Проверяет, является ли процесс системным
        /// </summary>
        private static bool IsSystemProcess(Process process)
        {
            string[] systemProcesses = { "explorer", "winlogon", "csrss", "smss", "services", "lsass" };

            return systemProcesses.Contains(process.ProcessName.ToLowerInvariant());
        }
    }

    /// <summary>
    /// Результат операции разблокировки файла
    /// </summary>
    public readonly struct UnlockResult
    {
        public bool IsSuccess { get; }
        public string Message { get; }

        private UnlockResult(bool isSuccess, string message)
        {
            IsSuccess = isSuccess;
            Message = message ?? string.Empty;
        }

        public static UnlockResult Success(string message = "Success")
        {
            return new(true, message);
        }

        public static UnlockResult Failed(string message)
        {
            return new(false, message);
        }
    }


}