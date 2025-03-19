using RevitBIMTool.Models;
using RevitBIMTool.Utils.Common;
using Serilog;
using System.IO;

namespace RevitBIMTool.Utils.ExportPDF
{
    internal static class RevitExportFileTracker
    {
        /// <summary>
        /// Находит и обрабатывает файл, созданный Revit после экспорта
        /// </summary>
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

            // Проверяем, содержит ли имя файла номер листа
            string sheetNumber = model.StringNumber;

            foreach (string pdfFile in Directory.GetFiles(exportFolder, "*.pdf"))
            {
                string fileName = Path.GetFileNameWithoutExtension(pdfFile);

                // Если имя файла содержит номер листа и файл свежий (создан недавно)
                if (fileName.Contains(sheetNumber) && IsRecentFile(pdfFile))
                {
                    Log.Information("Found matching file: {FileName}", Path.GetFileName(pdfFile));

                    // Пытаемся переименовать в ожидаемое имя
                    if (TryRenameFile(pdfFile, expectedFilePath))
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
        public static bool IsRecentFile(string filePath)
        {
            if (!File.Exists(filePath))
            {
                return false;
            }

            DateTime fileTimeUtc = File.GetLastWriteTimeUtc(filePath);
            TimeSpan timeElapsed = DateTime.UtcNow - fileTimeUtc;
            return timeElapsed.TotalMinutes <= 2;
        }

        /// <summary>
        /// Переименовывает файл с обработкой ошибок
        /// </summary>
        public static bool TryRenameFile(string sourcePath, string targetPath)
        {
            try
            {
                File.Move(sourcePath, targetPath);
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