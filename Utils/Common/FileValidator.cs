using System.IO;
using System.Text;

namespace RevitBIMTool.Utils.Common
{
    public static class FileValidator
    {
        /// <summary>
        /// Checks if file exists and has minimum size
        /// </summary>
        public static bool IsValid(string filePath, out string message, long minSizeBytes = 100)
        {
            if (string.IsNullOrEmpty(filePath))
            {
                message = "File path is null or empty";
                return false;
            }

            FileInfo fileInfo = new(filePath);

            if (!fileInfo.Exists)
            {
                message = $"File does not exist: {filePath}";
                return false;
            }

            if (fileInfo.Length < minSizeBytes)
            {
                message = $"File size: {fileInfo.Length} bytes";
                return false;
            }

            try
            {
                File.GetAttributes(filePath);
            }
            catch (Exception ex)
            {
                message = $"Error: {ex.Message}";
                return false;
            }

            message = null;
            return true;
        }

        /// <summary>
        /// Checks if file has been modified recently
        /// </summary>
        public static bool IsRecent(string filePath, out string message, int daysSpan = 1)
        {
            if (IsValid(filePath, out message))
            {
                StringBuilder sb = new();

                DateTime currentDate = DateTime.Now;
                DateTime lastModified = File.GetLastWriteTime(filePath);
                TimeSpan sinceModified = currentDate - lastModified;

                sb.AppendLine($"Current date: {currentDate:yyyy-MM-dd HH:mm:ss}");
                sb.AppendLine($"Last modified: {lastModified:yyyy-MM-dd HH:mm:ss}");
                sb.AppendLine($"Time elapsed since last change: {sinceModified.TotalDays} days");

                message = sb.ToString();

                return sinceModified.TotalDays < daysSpan;
            }

            return false;
        }

        /// <summary>
        /// Checks if one file is newer than another
        /// </summary>
        public static bool IsNewer(string filePath, string referencePath, out string message, int thresholdMinutes = 5)
        {
            StringBuilder sb = new();

            if (!IsValid(filePath, out string validMessage))
            {
                sb.AppendLine(validMessage);
                message = sb.ToString();
                return false;
            }

            if (!File.Exists(referencePath))
            {
                sb.AppendLine($"Reference file does not exist: {referencePath}");
                message = sb.ToString();
                return false;
            }

            DateTime fileDate = File.GetLastWriteTime(filePath);
            DateTime referenceDate = File.GetLastWriteTime(referencePath);
            TimeSpan difference = fileDate - referenceDate;

            bool result = difference.TotalMinutes > thresholdMinutes;

            sb.AppendLine($"File date: {fileDate:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine($"Reference date: {referenceDate:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine($"Time difference: {difference.TotalMinutes:F2} minutes");
            sb.AppendLine($"Threshold: {thresholdMinutes} minutes");
            sb.AppendLine($"File is newer: {result}");

            message = sb.ToString();
            return result;
        }

        /// <summary>
        /// Checks if target file is updated relative to source file
        /// </summary>
        public static bool IsUpdated(string targetPath, string sourcePath, out string message, int maxDaysOld = 100)
        {
            if (!IsValid(targetPath, out message))
            {
                return false;
            }

            return IsNewer(targetPath, sourcePath, out message) && IsRecent(targetPath, out message, maxDaysOld);
        }
    }
}