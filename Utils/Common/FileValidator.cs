using System.IO;
using System.Text;

namespace RevitBIMTool.Utils.Common;

public static class FileValidator
{
    /// <summary>
    /// Checks if file exists and has minimum size
    /// </summary>
    public static bool IsFileValid(string filePath, out string message, long minSizeBytes = 100)
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
            message = $"Is file valid!";
            return true;
        }
        catch (Exception ex)
        {
            message = $"Error: {ex.Message}";
            return false;
        }
    }

    /// <summary>
    /// Checks if file has been modified recently
    /// </summary>
    public static bool IsFileRecently(string filePath, out string message, int daysSpan = 1)
    {
        if (IsFileValid(filePath, out message))
        {
            StringBuilder sb = new();

            DateTime currentDate = DateTime.UtcNow;
            DateTime lastModified = File.GetLastWriteTimeUtc(filePath);
            TimeSpan sinceModified = currentDate - lastModified;

            sb.AppendLine($"Current date (UTC): {currentDate:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine($"Last modified (UTC): {lastModified:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine($"Time elapsed since last change: {sinceModified.TotalDays} days");

            message = sb.ToString();

            return sinceModified.TotalDays < daysSpan;
        }

        return false;
    }

    /// <summary>
    /// Checks if one file is newer than another
    /// </summary>
    public static bool IsFileNewer(string filePath, string referencePath, out string message, int thresholdMinutes = 15)
    {
        StringBuilder logBuilder = new();

        if (!IsFileValid(filePath, out string validMessage))
        {
            logBuilder.AppendLine(validMessage);
            message = logBuilder.ToString();
            return false;
        }

        DateTime fileDate = File.GetLastWriteTimeUtc(filePath);
        DateTime referenceDate = File.GetLastWriteTimeUtc(referencePath);
        TimeSpan difference = fileDate - referenceDate;

        bool result = difference.TotalMinutes > thresholdMinutes;

        logBuilder.AppendLine($"File date (UTC): {fileDate:yyyy-MM-dd HH:mm:ss}");
        logBuilder.AppendLine($"Reference date (UTC): {referenceDate:yyyy-MM-dd HH:mm:ss}");
        logBuilder.AppendLine($"Time difference: {difference.TotalMinutes:F2} minutes");
        logBuilder.AppendLine($"Threshold: {thresholdMinutes} minutes");
        logBuilder.AppendLine($"File is newer: {result}");

        message = logBuilder.ToString();
        return result;
    }

    /// <summary>
    /// Checks if target file is updated relative to source file
    /// </summary>
    public static bool IsUpdated(string targetPath, string sourcePath, out string message, int maxDaysOld = 100)
    {
        if (!IsFileValid(targetPath, out message))
        {
            return false;
        }

        return IsFileNewer(targetPath, sourcePath, out message) && IsFileRecently(targetPath, out message, maxDaysOld);
    }



}