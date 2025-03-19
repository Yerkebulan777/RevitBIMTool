using RevitBIMTool.Models;
using RevitBIMTool.Utils.Common;
using Serilog;
using System.IO;

namespace RevitBIMTool.Utils.ExportPDF
{
    /// <summary>
    /// Отслеживает и обрабатывает файлы, экспортированные Revit
    /// </summary>
    internal static class RevitExportFileTracker
    {
        /// <summary>
        /// Находит и обрабатывает файл, созданный Revit после экспорта
        /// </summary>
        /// <param name="expectedFilePath">Ожидаемый путь к файлу</param>
        /// <param name="exportFolder">Папка экспорта</param>
        /// <param name="model">Модель листа</param>
        /// <returns>True, если файл найден и обработан успешно</returns>
        public static bool TrackExportedFile(string expectedFilePath, string exportFolder, SheetModel model)
        {
            // Проверяем, существует ли файл с ожидаемым именем
            if (PathHelper.AwaitExistsFile(expectedFilePath))
            {
                Log.Information("File exported with expected name: {FileName}", Path.GetFileName(expectedFilePath));
                model.TempFilePath = expectedFilePath;
                model.IsSuccessfully = true;
                return true;
            }

            // Ищем любые PDF-файлы, созданные Revit в заданной папке
            string[] pdfFiles = Directory.GetFiles(exportFolder, "*.pdf");

            // Проверяем, содержит ли имя файла номер листа
            string sheetNumber = model.StringNumber;
            foreach (string pdfFile in pdfFiles)
            {
                string fileName = Path.GetFileNameWithoutExtension(pdfFile);

                // Если имя файла содержит номер листа и файл свежий (создан недавно)
                if (fileName.Contains(sheetNumber) && IsRecentFile(pdfFile))
                {
                    Log.Information("Found matching file: {FileName}", Path.GetFileName(pdfFile));

                    // Пытаемся переименовать в ожидаемое имя
                    if (RenameFile(pdfFile, expectedFilePath))
                    {
                        model.TempFilePath = expectedFilePath;
                    }
                    else
                    {
                        // Если переименование не удалось, используем найденный файл
                        model.TempFilePath = pdfFile;
                    }

                    model.IsSuccessfully = true;
                    return true;
                }
            }

            Log.Warning("No matching exported file found for sheet: {SheetNumber}", sheetNumber);
            return false;
        }

        /// <summary>
        /// Проверяет, создан ли файл недавно (последние 2 минуты)
        /// </summary>
        private static bool IsRecentFile(string filePath)
        {
            if (!File.Exists(filePath))
                return false;

            DateTime fileTimeUtc = File.GetLastWriteTimeUtc(filePath);
            TimeSpan timeElapsed = DateTime.UtcNow - fileTimeUtc;
            return timeElapsed.TotalMinutes <= 2;
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
                    File.Delete(targetPath);

                File.Move(sourcePath, targetPath);
                Log.Information("File renamed: {OldName} → {NewName}",
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
        /// Отслеживает новые файлы, созданные в папке во время экспорта
        /// </summary>
        public static string[] GetNewFiles(string folder, string[] existingFiles)
        {
            try
            {
                string[] currentFiles = Directory.GetFiles(folder, "*.pdf");
                return currentFiles.Except(existingFiles).ToArray();
            }
            catch (Exception ex)
            {
                Log.Error("Error while searching for new files: {Error}", ex.Message);
                return Array.Empty<string>();
            }
        }
    }
}