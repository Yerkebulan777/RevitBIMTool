using RevitBIMTool.Models;
using Serilog;
using System.IO;

namespace RevitBIMTool.Utils.ExportPDF
{
    /// <summary>
    /// Эффективный монитор файлов для отслеживания экспорта PDF из Revit
    /// </summary>
    internal static class ExportFileMonitor
    {
        /// <summary>
        /// Максимальное количество попыток найти файл
        /// </summary>
        private const int MaxAttempts = 5;

        /// <summary>
        /// Задержка между попытками в миллисекундах
        /// </summary>
        private const int DelayBetweenAttempts = 200;

        /// <summary>
        /// Словарь для отслеживания существующих файлов по каталогам
        /// </summary>
        private static readonly Dictionary<string, string[]> ExistingFilesByFolder = [];

        /// <summary>
        /// Запоминает существующие PDF-файлы в указанной папке
        /// </summary>
        public static void CaptureExistingFiles(string folderPath)
        {
            try
            {
                string[] files = Directory.GetFiles(folderPath, "*.pdf");
                ExistingFilesByFolder[folderPath] = files;
                Log.Debug("Captured {Count} existing PDF files in folder: {Folder}", files.Length, folderPath);
            }
            catch (Exception ex)
            {
                Log.Error("Error capturing existing files: {Error}", ex.Message);
                ExistingFilesByFolder[folderPath] = new string[0];
            }
        }

        /// <summary>
        /// Сбрасывает кэш существующих файлов
        /// </summary>
        public static void Reset()
        {
            ExistingFilesByFolder.Clear();
        }

        /// <summary>
        /// Находит экспортированный файл и обновляет модель листа
        /// </summary>
        /// <param name="model">Модель листа</param>
        /// <param name="processedModels">Список уже обработанных моделей (для проверки дубликатов)</param>
        /// <returns>True, если файл найден и обработан успешно</returns>
        public static bool FindExportedFile(SheetModel model, ref List<SheetModel> processedModels)
        {
            // Получаем пути из модели
            string expectedFilePath = model.TempFilePath;
            string exportFolder = Path.GetDirectoryName(expectedFilePath);

            // Добавляем расширение PDF, если его нет
            if (!expectedFilePath.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
            {
                expectedFilePath += ".pdf";
            }

            // Проверка на дубликаты по пути к временному файлу
            if (processedModels.Any(m => m.TempFilePath == expectedFilePath && m.IsSuccessfully))
            {
                Log.Debug("Sheet {SheetNumber} already processed", model.StringNumber);
                model.IsSuccessfully = true;
                return true;
            }

            // Быстрая проверка ожидаемого файла (без длительного ожидания)
            if (File.Exists(expectedFilePath))
            {
                Log.Information("File exported with expected name: {FileName}", Path.GetFileName(expectedFilePath));
                model.TempFilePath = expectedFilePath;
                model.IsSuccessfully = true;
                processedModels.Add(model);
                return true;
            }

            // Получаем список существующих файлов до экспорта
            string[] existingFiles = ExistingFilesByFolder.ContainsKey(exportFolder)
                ? ExistingFilesByFolder[exportFolder]
                : new string[0];

            // Эффективный поиск нового файла с ограниченным числом попыток
            for (int attempt = 0; attempt < MaxAttempts; attempt++)
            {
                string newFile = FindNewFile(exportFolder, existingFiles);

                if (!string.IsNullOrEmpty(newFile))
                {
                    Log.Information("Found new file at attempt {Attempt}: {FileName}",
                        attempt + 1, Path.GetFileName(newFile));

                    // Пытаемся переименовать в ожидаемое имя
                    if (RenameFile(newFile, expectedFilePath))
                    {
                        model.TempFilePath = expectedFilePath;
                    }
                    else
                    {
                        // Если переименование не удалось, используем найденный файл
                        model.TempFilePath = newFile;
                    }

                    model.IsSuccessfully = true;
                    processedModels.Add(model);
                    return true;
                }

                // Короткая пауза перед следующей попыткой
                if (attempt < MaxAttempts - 1)
                {
                    System.Threading.Thread.Sleep(DelayBetweenAttempts);
                }
            }

            Log.Warning("No new file found after {Attempts} attempts for sheet: {SheetNumber}",
                MaxAttempts, model.StringNumber);
            return false;
        }

        /// <summary>
        /// Переименовывает файл с обработкой ошибок
        /// </summary>
        private static bool RenameFile(string sourcePath, string targetPath)
        {
            try
            {
                // Удаляем целевой файл, если он существует
                if (File.Exists(targetPath))
                {
                    File.Delete(targetPath);
                }

                File.Move(sourcePath, targetPath);
                Log.Debug("File renamed: {OldName} → {NewName}",
                    Path.GetFileName(sourcePath),
                    Path.GetFileName(targetPath));
                return true;
            }
            catch (Exception ex)
            {
                Log.Error("Failed to rename file: {Error}", ex.Message);
                return false;
            }
        }

        /// <summary>
        /// Находит один новый файл, появившийся в папке после экспорта
        /// </summary>
        /// <returns>Путь к найденному файлу или null, если файл не найден</returns>
        private static string FindNewFile(string folder, string[] existingFiles)
        {
            try
            {
                string[] currentFiles = Directory.GetFiles(folder, "*.pdf");

                // Находим и возвращаем первый новый файл
                foreach (string file in currentFiles)
                {
                    if (!existingFiles.Contains(file))
                    {
                        return file;
                    }
                }

                return null;
            }
            catch (Exception ex)
            {
                Log.Error("Error searching for new file: {Error}", ex.Message);
                return null;
            }
        }
    }
}