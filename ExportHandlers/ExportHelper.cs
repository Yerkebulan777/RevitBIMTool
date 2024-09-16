using RevitBIMTool.Utils;
using RevitBIMTool.Utils.SystemUtil;
using Serilog;
using ServiceLibrary.Helpers;
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

        RevitPathHelper.EnsureDirectory(exportDirectory);

        return exportDirectory;
    }


    public static bool IsFileUpdated(string targetPath, string sourcePath, int limit = 100)
    {
        FileInfo targetFile = new(targetPath);

        DateTime currentDate = DateTime.Now;

        if (targetFile.Exists && targetFile.Length > limit)
        {
            DateTime targetLastDate = File.GetLastWriteTime(targetPath);
            DateTime sourceLastDate = File.GetLastWriteTime(sourcePath);

            TimeSpan targetDifference = currentDate - targetLastDate;
            TimeSpan sourceDifference = targetLastDate - sourceLastDate;

            bool isTargetLimitGreate = targetDifference.TotalDays > limit;
            bool isSourceLimitGreate = sourceDifference.TotalSeconds > limit;

            Log.Debug($"Target last write: {targetLastDate:yyyy-MM-dd}");
            Log.Debug($"Source last write: {sourceLastDate:yyyy-MM-dd}");

            Log.Debug($"Target difference in days: {Math.Round(targetDifference.TotalDays)}");
            Log.Debug($"Source difference in days: {Math.Round(sourceDifference.TotalDays)}");

            if (isSourceLimitGreate && isTargetLimitGreate)
            {
                Log.Debug($"Is updated file!");
                return true;
            }
        }

        return false;
    }


    public static void CreateZipTheFolder(string exportFolder, string exportDirectory)
    {
        Log.Debug("Start create Zip folder... ");

        string zipFilePath = $"{exportFolder}.zip";

        DirectoryInfo directory = new(exportFolder);

        SystemFolderOpener.OpenFolder(exportDirectory);

        using ZipArchive archive = ZipFile.Open(zipFilePath, ZipArchiveMode.Create);

        foreach (FileInfo info in directory.EnumerateFiles())
        {
            if (PathHelper.IsFileAccessible(info.FullName))
            {
                if (info.Length > 0)
                {
                    try
                    {
                        _ = archive.CreateEntryFromFile(info.FullName, info.Name);
                    }
                    catch (Exception ex)
                    {
                        Log.Error($"Failed: {info.Name} {ex.Message}");
                    }
                }
            }
        }

    }

}