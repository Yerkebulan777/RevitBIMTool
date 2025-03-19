using Serilog;
using System.IO;

namespace RevitBIMTool.Utils.Common
{
    /// <summary>
    /// Объединенный класс для работы с файлами, проверки их валидности и мониторинга
    /// </summary>
    public static class FileValidator
    {

        #region Basic Validation

        /// <summary>
        /// Проверяет, был ли файл изменен в указанный период времени
        /// </summary>
        public static bool IsFileRecent(string filePath, TimeSpan timeSpan)
        {
            try
            {
                DateTime fileTimeUtc = File.GetLastWriteTimeUtc(filePath);
                TimeSpan timeElapsed = DateTime.UtcNow - fileTimeUtc;
                return timeElapsed < timeSpan;
            }
            catch (Exception)
            {
                Log.Error("IsFileRecent: {File}", filePath);
                return false;
            }
        }

        /// <summary>
        /// Проверяет, существует ли файл и имеет ли он минимальный размер
        /// </summary>
        public static bool IsFileValid(string filePath, long minSizeBytes = 100)
        {
            try
            {
                FileInfo fileInfo = new(filePath);

                if (!fileInfo.Exists)
                {
                    Log.Warning("Missing file: {Path}", Path.GetFileName(filePath));
                    return false;
                }

                if (fileInfo.Length < minSizeBytes)
                {
                    Log.Warning("Small file: {Size}b", fileInfo.Length);
                    return false;
                }

                File.GetAttributes(filePath);
                return true;
            }
            catch (Exception ex)
            {
                Log.Error(ex, ex.Message);
                return false;
            }
        }

        /// <summary>
        /// Проверяет, новее ли один файл другого
        /// </summary>
        public static bool IsFileNewer(string filePath, string referencePath, int minutes = 30)
        {
            if (IsFileValid(filePath))
            {
                try
                {
                    DateTime fileDate = File.GetLastWriteTimeUtc(filePath);
                    DateTime refDate = File.GetLastWriteTimeUtc(referencePath);
                    double diffMin = (fileDate - refDate).TotalMinutes;
                    return diffMin > minutes;
                }
                catch (Exception)
                {
                    Log.Error("Date comparison failed: {File}", filePath);
                    return false;
                }
            }

            return false;
        }

        /// <summary>
        /// Проверяет, обновлен ли целевой файл относительно исходного
        /// </summary>
        public static bool IsUpdated(string targetPath, string sourcePath, int maxDaysOld = 100)
        {
            if (IsFileValid(targetPath) && IsFileNewer(targetPath, sourcePath))
            {
                Log.Debug("Updated: {File}", Path.GetFileName(targetPath));
                TimeSpan timeSpan = TimeSpan.FromDays(maxDaysOld);
                return IsFileRecent(targetPath, timeSpan);
            }
            return false;
        }

        #endregion


        #region File Monitoring

        private static string FindMatchingFile(string folder, string[] existingFiles, Func<string, bool> matchPredicate)
        {
            string[] currentFiles = Directory.GetFiles(folder, "*.pdf");

            foreach (string file in currentFiles.Except(existingFiles))
            {
                if (matchPredicate(file))
                {
                    return file;
                }
            }
            return null;
        }

        public static bool VerifyFile(string expectedFilePath, Func<string, bool> matchPredicate, out string resultPath, int timeoutMs = 300)
        {
            resultPath = null;
            string exportFolder = Path.GetDirectoryName(expectedFilePath);
            string[] existingFiles = Directory.GetFiles(exportFolder, "*.pdf");
            string fileName = Path.GetFileName(expectedFilePath);

            Log.Debug("Tracking file: {FileName}", fileName);

            int attempt = 1;

            while (attempt < 1000)
            {
                try
                {
                    // Проверка ожидаемого файла
                    if (IsFileValid(expectedFilePath))
                    {
                        resultPath = expectedFilePath;
                        return true;
                    }

                    // Составляем предикат, включающий проверку на свежесть файла и соответствие имени
                    bool combinedPredicate(string file) =>
                        IsRecentFile(file) && (Path.GetFileName(file) == fileName || matchPredicate(file));

                    string matchedFile = FindMatchingFile(exportFolder, existingFiles, combinedPredicate);

                    if (matchedFile != null)
                    {
                        resultPath = matchedFile != expectedFilePath && TryRenameFile(matchedFile, expectedFilePath)
                            ? expectedFilePath
                            : matchedFile;

                        return true;
                    }

                    // Обновляем список существующих файлов
                    existingFiles = Directory.GetFiles(exportFolder, "*.pdf");
                }
                catch (Exception)
                {
                    // Игнорируем ошибки и продолжаем
                }

                Thread.Sleep(timeoutMs);
                attempt++;
            }

            return false;
        }



        /// <summary>
        /// Переименовывает файл с обработкой ошибок
        /// </summary>
        public static bool TryRenameFile(string sourcePath, string targetPath)
        {
            try
            {
                // Если целевой файл уже существует, удаляем его
                if (File.Exists(targetPath))
                {
                    File.Delete(targetPath);
                }

                File.Move(sourcePath, targetPath);
                Log.Debug("Renamed: {Old} → {New}",
                    Path.GetFileName(sourcePath), Path.GetFileName(targetPath));
                return true;
            }
            catch (Exception)
            {
                Log.Error("Rename failed: {File}", Path.GetFileName(sourcePath));
                return false;
            }
        }

        #endregion
    }
}