using RevitBIMTool.Utils;
using RevitBIMTool.Utils.SystemUtil;
using Serilog;
using ServiceLibrary.Helpers;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;


namespace RevitBIMTool.ExportHandlers;
internal static class ExportHelper
{

    public static string SetDirectory(string revitFilePath, string folderName, bool date)
    {
        string exportDirectory = RevitPathHelper.DetermineDirectory(revitFilePath, folderName);

        if (string.IsNullOrEmpty(exportDirectory))
        {
            exportDirectory = Path.Combine(Path.GetDirectoryName(revitFilePath), folderName);
        }

        if (date)
        {
            string formatedDate = DateTime.Today.ToString("yyyy-MM-dd");
            exportDirectory = Path.Combine(exportDirectory, formatedDate);
        }

        return exportDirectory;
    }


    public static bool IsFileUpdated(string targetPath, string sourcePath, int limit = 100)
    {
        bool isUpdated = false;

        FileInfo targetFile = new(targetPath);

        DateTime currentDateTime = DateTime.Now;

        if (targetFile.Exists && targetFile.Length > limit)
        {
            DateTime targetLastDate = File.GetLastWriteTime(targetPath);
            DateTime sourceLastDate = File.GetLastWriteTime(sourcePath);

            TimeSpan sourceDifference = targetLastDate - sourceLastDate;
            TimeSpan currentDifference = currentDateTime - targetLastDate;

            Debug.WriteLine($"Target last write: {targetLastDate:yyyy-MM-dd}");
            Debug.WriteLine($"Source last write: {sourceLastDate:yyyy-MM-dd}");

            Debug.WriteLine($"Source difference in days: {sourceDifference.TotalDays}");
            Debug.WriteLine($"Current difference in days: {currentDifference.TotalDays}");

            bool isSourceTimeGreate = sourceDifference.TotalSeconds > limit;
            bool isCurrentTimeGreate = currentDifference.TotalDays > limit;

            if (isSourceTimeGreate && isCurrentTimeGreate)
            {
                isUpdated = true;
            }
        }

        Debug.WriteLine($"Is updated: {isUpdated}");

        return isUpdated;
    }


    public static string GetRelativePath(string fromPath, string toPath)
    {
        Uri fromUri = new(fromPath);
        Uri toUri = new(toPath);

        Uri relativeUri = fromUri.MakeRelativeUri(toUri);
        string relativePath = Uri.UnescapeDataString(relativeUri.ToString());

        return relativePath.Replace('/', Path.DirectorySeparatorChar);
    }


    public static void CreateZipTheFolder(string revitFileName, string exportDirectory)
    {
        string exportFolder = Path.Combine(exportDirectory, revitFileName);

        SystemFolderOpener.OpenFolder(exportDirectory);

        if (PathHelper.IsFileAccessible(exportFolder))
        {
            using ZipArchive archive = ZipFile.Open($"{exportFolder}.zip", ZipArchiveMode.Create);

            foreach (string filePath in Directory.GetFiles(exportFolder))
            {
                FileInfo info = new(filePath);

                if (info.Length > 0)
                {
                    try
                    {
                        _ = archive.CreateEntryFromFile(filePath, info.Name, CompressionLevel.Fastest);
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Failed: {info.Name} {ex.Message}");
                    }

                }
            }

        }
    }


}