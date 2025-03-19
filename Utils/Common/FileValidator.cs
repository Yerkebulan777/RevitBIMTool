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

                _ = File.GetAttributes(filePath);
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

        /// <summary>
        /// Проверяет наличие файла, используя составной предикат по имени и новизне файла, а также его валидности.
        /// </summary>
        public static bool VerifyFile(ref List<string> existingFiles, string expectedFilePath)
        {
            bool combinedPredicate(string file)
            {
                string fileName = Path.GetFileName(expectedFilePath);
                return (Path.GetFileName(file) == fileName || IsFileRecent(file, TimeSpan.FromMinutes(30))) && IsFileValid(file);
            }

            return VerifyFile(ref existingFiles, expectedFilePath, combinedPredicate, 300);
        }

        /// <summary>
        /// Выполняет проверку файла с использованием переданного предиката, пытаясь найти и переименовать файл в ожидаемый путь.
        /// </summary>
        public static bool VerifyFile(ref List<string> existingFiles, string expectedFilePath, Func<string, bool> matchPredicate, int timeout)
        {
            string folderPath = Path.GetDirectoryName(expectedFilePath);
            string fileName = Path.GetFileName(expectedFilePath);

            Log.Debug("Verify file: {FileName}", fileName);

            int attempt = 0;

            while (attempt < 1000)
            {
                try
                {
                    attempt++;

                    string matchedFile = FindMatch(folderPath, existingFiles, matchPredicate);

                    if (matchedFile != null)
                    {
                        existingFiles.Add(matchedFile);
                        Log.Information("Match found: {File}", matchedFile);
                        return TryRenameFile(matchedFile, expectedFilePath);
                    }
                }
                finally
                {
                    Thread.Sleep(timeout);
                }
            }

            return false;
        }

        /// <summary>
        /// Ищет в указанной папке PDF-файлы, не находящиеся в списке существующих файлов, и возвращает первый соответствующий предикату.
        /// </summary>
        private static string FindMatch(string folder, List<string> existingFiles, Func<string, bool> matchPredicate)
        {
            return Directory.GetFiles(folder, "*.pdf").Except(existingFiles).FirstOrDefault(matchPredicate);
        }

        /// <summary>
        /// Переименовывает файл с обработкой ошибок
        /// </summary>
        public static bool TryRenameFile(string sourcePath, string targetPath)
        {
            if (sourcePath != targetPath)
            {
                try
                {
                    File.Move(sourcePath, targetPath);
                }
                catch (Exception ex)
                {
                    throw new IOException("Rename failed", ex);
                }
            }

            return true;
        }

        #endregion
    }
}