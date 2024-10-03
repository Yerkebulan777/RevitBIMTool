using RevitBIMTool.Utils;
using RevitBIMTool.Utils.SystemHelpers;
using Serilog;
using ServiceLibrary.Helpers;
using System.IO;
using System.IO.Compression;
using System.Text;


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


    public static bool IsFileUpdated(string targetPath, string sourcePath, out string output, int limitDays = 100)
    {
        bool result = false;
        FileInfo targetFile = new(targetPath);
        StringBuilder sb = new StringBuilder();

        if (targetFile.Exists && targetFile.Length > 100)
        {
            DateTime targetLastDate = File.GetLastWriteTime(targetPath);
            DateTime sourceLastDate = File.GetLastWriteTime(sourcePath);

            sb.AppendLine($"Target last write: {targetLastDate:yyyy-MM-dd}");
            sb.AppendLine($"Source last write: {sourceLastDate:yyyy-MM-dd}");

            if (targetLastDate > sourceLastDate)
            {
                DateTime currentNowDate = DateTime.Now;

                TimeSpan sourceDifference = currentNowDate - sourceLastDate;
                TimeSpan targetDifference = currentNowDate - targetLastDate;

                sb.AppendLine($"Source difference in days: {sourceDifference.Days}");
                sb.AppendLine($"Target difference in days: {targetDifference.Days}");

                result = limitDays > targetDifference.Days;
            }
        }

        sb.AppendLine($"Is updated file: {result}");
        output = sb.ToString();
        return result;
    }


    public static void CreateZipFolder(string exportFolder, string exportDirectory)
    {
        string zipFilePath = $"{exportFolder}.zip";

        DirectoryInfo directory = new(exportFolder);

        SystemFolderOpener.OpenFolder(exportDirectory);

        Log.Debug($"Start create Zip file by path {zipFilePath}");

        using ZipArchive archive = ZipFile.Open(zipFilePath, ZipArchiveMode.Create);

        foreach (FileInfo info in directory.EnumerateFiles())
        {
            if (FilePathHelper.IsFileAccessible(info.FullName))
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