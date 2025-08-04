using Autodesk.Revit.UI;
using System.IO;
using System.Reflection;
using System.Text;

namespace CommonUtils
{
    public static class EnhancedResourceDiagnostic
    {
        /// <summary>
        /// Расширенная диагностика, которая помогает понять, 
        /// почему ресурсы не встраиваются в сборку
        /// </summary>
        public static void DiagnoseEmbeddingIssues()
        {
            try
            {
                Assembly currentAssembly = Assembly.GetExecutingAssembly();
                string[] resources = currentAssembly.GetManifestResourceNames();
                string assemblyPath = currentAssembly.Location;

                StringBuilder report = new();

                // Основная информация о сборке
                _ = report.AppendLine("=== ДИАГНОСТИКА ВСТРАИВАНИЯ РЕСУРСОВ ===");
                _ = report.AppendLine($"Имя сборки: {Path.GetFileName(assemblyPath)}");
                _ = report.AppendLine($"Путь к сборке: {Directory.GetParent(assemblyPath)}");
                _ = report.AppendLine($"Время создания: {GetFileCreationTime(assemblyPath)}");
                _ = report.AppendLine($"Размер сборки: {GetFileSizeInKB(assemblyPath)} KB");

                _ = report.AppendLine();

                if (resources.Length == 0)
                {
                    _ = report.AppendLine("Ресурсы не встроены!");
                    _ = report.AppendLine("Возможные причины:");
                    _ = report.AppendLine("1. Файлы не существуют в указанном пути");
                    _ = report.AppendLine("2. Build Action не установлен в 'Embedded Resource'");
                    _ = report.AppendLine("3. Ошибка в пути в файле .csproj");
                    _ = report.AppendLine("4. Файлы не включены в проект");
                    _ = report.AppendLine();
                    _ = report.AppendLine("Рекомендации по исправлению:");
                    _ = report.AppendLine("- Проверьте физическое существование файлов");
                    _ = report.AppendLine("- Убедитесь, что Build Action = Embedded Resource");
                    _ = report.AppendLine("- Попробуйте разместить файлы в корне проекта");
                }
                else
                {
                    _ = report.AppendLine($"Найдено ресурсов: {resources.Length}");
                    foreach (string resource in resources)
                    {
                        _ = report.AppendLine($"- {resource}");
                    }
                }

                // Показываем результат
                TaskDialog dialog = new("Диагностика встраивания")
                {
                    MainContent = report.ToString()
                };
                _ = dialog.Show();

                System.Diagnostics.Debug.WriteLine(report.ToString());
            }
            catch (Exception ex)
            {
                _ = TaskDialog.Show("Ошибка", $"Ошибка диагностики: {ex.Message}");
            }
        }

        /// <summary>
        /// Получает размер файла в килобайтах для анализа
        /// </summary>
        private static long GetFileSizeInKB(string filePath)
        {
            try
            {
                return new System.IO.FileInfo(filePath).Length / 1024;
            }
            catch
            {
                return 0;
            }
        }

        /// <summary>
        /// Получает время создания файла для понимания, когда была последняя компиляция
        /// </summary>
        private static string GetFileCreationTime(string filePath)
        {
            try
            {
                return System.IO.File.GetLastWriteTime(filePath).ToString("yyyy-MM-dd HH:mm:ss");
            }
            catch
            {
                return "Неизвестно";
            }
        }
    }
}
