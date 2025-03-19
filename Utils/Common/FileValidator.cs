using RevitBIMTool.Models;
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
        /// Проверяет, существует ли файл и имеет ли он минимальный размер
        /// </summary>
        public static bool IsFileValid(string filePath, long minSizeBytes = 100)
        {
            if (string.IsNullOrEmpty(filePath))
            {
                Log.Warning("Empty path");
                return false;
            }

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

                // Проверка доступности файла
                _ = File.GetAttributes(filePath);
                Log.Debug("Valid: {File}", Path.GetFileName(filePath));
                return true;
            }
            catch (Exception)
            {
                Log.Error("Validation failed: {File}", Path.GetFileName(filePath));
                return false;
            }
        }

        /// <summary>
        /// Проверяет, был ли файл недавно изменен
        /// </summary>
        public static bool IsFileRecently(string filePath, int daysSpan = 1)
        {
            if (!IsFileValid(filePath))
            {
                return false;
            }

            try
            {
                DateTime lastModified = File.GetLastWriteTimeUtc(filePath);
                TimeSpan sinceModified = DateTime.UtcNow - lastModified;

                Log.Debug("File age: {File} - {Days}d", Path.GetFileName(filePath), Math.Round(sinceModified.TotalDays, 1));

                return sinceModified.TotalDays < daysSpan;
            }
            catch (Exception)
            {
                Log.Error("Age check failed: {File}", Path.GetFileName(filePath));
                return false;
            }
        }

        /// <summary>
        /// Проверяет, новее ли один файл другого
        /// </summary>
        public static bool IsFileNewer(string filePath, string referencePath, int thresholdMinutes = 15)
        {
            if (!IsFileValid(filePath))
            {
                return false;
            }

            try
            {
                DateTime fileDate = File.GetLastWriteTimeUtc(filePath);
                DateTime refDate = File.GetLastWriteTimeUtc(referencePath);
                double diffMin = (fileDate - refDate).TotalMinutes;
                bool result = diffMin > thresholdMinutes;

                Log.Debug("Compare: {File} vs ref, diff: {Diff}min, result: {Result}",
                    Path.GetFileName(filePath), Math.Round(diffMin, 1), result);

                return result;
            }
            catch (Exception)
            {
                Log.Error("Date comparison failed: {File}", Path.GetFileName(filePath));
                return false;
            }
        }

        /// <summary>
        /// Проверяет, обновлен ли целевой файл относительно исходного
        /// </summary>
        public static bool IsUpdated(string targetPath, string sourcePath, int maxDaysOld = 100)
        {
            if (!IsFileValid(targetPath))
            {
                return false;
            }

            return IsFileNewer(targetPath, sourcePath) && IsFileRecently(targetPath, maxDaysOld);
        }

        #endregion

        #region File Monitoring

        /// <summary>
        /// Ожидает и отслеживает создание файла, работая как с прямым путем, так и с поиском по шаблонам
        /// </summary>
        /// <param name="expectedFilePath">Ожидаемый путь к файлу</param>
        /// <param name="exportFolder">Папка для поиска альтернативных файлов</param>
        /// <param name="model">Модель листа, для которой отслеживается файл</param>
        /// <param name="timeoutMs">Таймаут между проверками в мс</param>
        /// <param name="attempts">Максимальное число попыток</param>
        /// <returns>true, если файл найден или создан</returns>
        public static bool VerifyFile(string expectedFilePath, string exportFolder, SheetModel model, int timeoutMs = 300, int attempts = 60)
        {
            int count = 0;

            string[] existingFiles = Directory.Exists(exportFolder)
                ? Directory.GetFiles(exportFolder, "*.pdf")
                : Array.Empty<string>();

            string sheetNumber = model.StringNumber;
            Log.Debug("Tracking file for sheet: {Sheet}", sheetNumber);

            while (count < attempts)
            {
                count++;

                // Проверка ожидаемого пути к файлу
                if (IsFileValid(expectedFilePath))
                {
                    Log.Information("Found expected file: {File}", Path.GetFileName(expectedFilePath));
                    model.TempFilePath = expectedFilePath;
                    model.IsSuccessfully = true;
                    return true;
                }

                // Поиск новых подходящих файлов в папке
                try
                {
                    // Получаем новые файлы, созданные после начала отслеживания
                    string[] currentFiles = Directory.GetFiles(exportFolder, "*.pdf");
                    string[] newFiles = currentFiles.Except(existingFiles).ToArray();

                    foreach (string pdfFile in newFiles)
                    {
                        // Проверяем, создан ли файл недавно
                        if (!IsRecentFile(pdfFile))
                        {
                            continue;
                        }

                        string fileName = Path.GetFileNameWithoutExtension(pdfFile);

                        // Проверяем, содержит ли имя файла номер листа
                        if (fileName.Contains(sheetNumber) ||
                            (Path.GetFileName(pdfFile) == Path.GetFileName(expectedFilePath)))
                        {
                            Log.Information("Found match: {File} (attempt {Try})",
                                Path.GetFileName(pdfFile), count);

                            // Пытаемся переименовать файл в ожидаемое имя
                            model.TempFilePath = pdfFile != expectedFilePath && TryRenameFile(pdfFile, expectedFilePath) ? expectedFilePath : pdfFile;

                            model.IsSuccessfully = true;
                            return true;
                        }
                    }

                    // Обновляем список существующих файлов
                    existingFiles = currentFiles;
                }
                catch (Exception ex)
                {
                    Log.Debug("Search attempt {Try} failed: {Error}", count, ex.Message);
                }

                Thread.Sleep(timeoutMs);
            }

            Log.Warning("File not found after {Max} count for sheet: {Sheet}",
                attempts, sheetNumber);
            return false;
        }

        /// <summary>
        /// Проверяет, создан ли файл недавно (последние 2 минуты)
        /// </summary>
        public static bool IsRecentFile(string filePath, int recentMinutes = 2)
        {
            if (!File.Exists(filePath))
            {
                return false;
            }

            try
            {
                DateTime fileTimeUtc = File.GetLastWriteTimeUtc(filePath);
                TimeSpan timeElapsed = DateTime.UtcNow - fileTimeUtc;
                return timeElapsed.TotalMinutes < recentMinutes;
            }
            catch (Exception)
            {
                Log.Error("Recency check failed: {File}", Path.GetFileName(filePath));
                return false;
            }
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

        /// <summary>
        /// Получает список новых файлов, созданных в папке после предыдущей проверки
        /// </summary>
        public static string[] GetNewFiles(string folder, string[] existingFiles, string pattern = "*.pdf")
        {
            try
            {
                if (!Directory.Exists(folder))
                {
                    Log.Warning("Dir missing: {Dir}", Path.GetFileName(folder));
                    return Array.Empty<string>();
                }

                string[] currentFiles = Directory.GetFiles(folder, pattern);
                return currentFiles.Except(existingFiles).ToArray();
            }
            catch (Exception)
            {
                Log.Error("File listing failed: {Dir}", Path.GetFileName(folder));
                return Array.Empty<string>();
            }
        }

        #endregion
    }
}