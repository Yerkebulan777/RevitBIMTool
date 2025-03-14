using System.IO;
using System.Text;

namespace RevitBIMTool.Utils.Common
{
    /// <summary>
    /// Статический класс для проверки валидности и актуальности файлов
    /// </summary>
    public static class FileValidator
    {
        /// <summary>
        /// Проверяет существование файла и его минимальный размер
        /// </summary>
        public static bool IsValid(string filePath, long minSizeBytes = 100)
        {
            if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
            {
                return false;
            }

            FileInfo fileInfo = new FileInfo(filePath);
            return fileInfo.Length >= minSizeBytes;
        }

        /// <summary>
        /// Проверяет актуальность файла на основе времени последнего изменения
        /// </summary>
        public static bool IsRecent(string filePath, int maxDaysOld = 7)
        {
            if (!IsValid(filePath))
            {
                return false;
            }

            DateTime lastModified = File.GetLastWriteTime(filePath);
            TimeSpan age = DateTime.Now - lastModified;

            return age.TotalDays <= maxDaysOld;
        }

        /// <summary>
        /// Проверяет актуальность целевого файла относительно исходного
        /// </summary>
        public static bool IsUpdated(string targetPath, string sourcePath, out string output, int maxDaysOld = 100)
        {
            bool result = false;
            StringBuilder sb = new StringBuilder();

            if (IsValid(targetPath))
            {
                DateTime targetLastDate = File.GetLastWriteTime(targetPath);
                DateTime sourceLastDate = File.GetLastWriteTime(sourcePath);

                sb.AppendLine($"Target last write: {targetLastDate:yyyy-MM-dd}");
                sb.AppendLine($"Source last write: {sourceLastDate:yyyy-MM-dd}");

                if (targetLastDate > sourceLastDate)
                {
                    DateTime currentDate = DateTime.Now;

                    TimeSpan sourceAge = currentDate - sourceLastDate;
                    TimeSpan targetAge = currentDate - targetLastDate;

                    sb.AppendLine($"Source difference in days: {sourceAge.Days}");
                    sb.AppendLine($"Target difference in days: {targetAge.Days}");

                    result = targetAge.Days < maxDaysOld;
                }
            }

            sb.AppendLine($"Is updated file: {result}");
            output = sb.ToString();

            return result;
        }
    }
}