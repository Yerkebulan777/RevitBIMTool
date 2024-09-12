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


    public static bool IsTargetFileUpdated(string targetFilePath, string sourceFilePath, int minimum = 100)
    {
        bool isTargetFileUpdated = false;

        if (File.Exists(targetFilePath) && File.Exists(sourceFilePath))
        {
            bool targetAcessible = PathHelper.IsFileAccessible(targetFilePath);

            DateTime targetFileDate = File.GetLastWriteTime(targetFilePath);
            DateTime sourceFileDate = File.GetLastWriteTime(sourceFilePath);

            TimeSpan timeDifference = targetFileDate - sourceFileDate;
            long targetFileSize = new FileInfo(targetFilePath).Length;

            Log.Debug($"Target file: {Path.GetFileName(targetFilePath)}");
            Log.Debug($"Difference: {timeDifference:dd.MM.yyyy HH:mm}");

            bool isUpdated = timeDifference.TotalSeconds < minimum;
            bool isIndated = timeDifference.TotalDays < minimum;

            bool isFileSizeValid = targetFileSize > minimum;
            bool isModifiedValid = isUpdated && isIndated;

            if (isModifiedValid && isFileSizeValid)
            {
                isTargetFileUpdated = true;
            }
        }

        Log.Debug($"Is updated: {isTargetFileUpdated}");

        return isTargetFileUpdated;
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