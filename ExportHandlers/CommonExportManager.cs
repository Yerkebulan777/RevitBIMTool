using RevitBIMTool.Utils.Common;
using RevitBIMTool.Utils.SystemHelpers;
using Serilog;
using ServiceLibrary.Helpers;
using System.IO;
using System.IO.Compression;

namespace RevitBIMTool.ExportHandlers;

internal static class CommonExportManager
{
    public static string SetDirectory(string revitFilePath, string folderName, bool date)
    {
        string exportDirectory = PathHelper.DetermineDirectory(revitFilePath, folderName);

        if (string.IsNullOrEmpty(exportDirectory))
        {
            exportDirectory = Path.Combine(Path.GetDirectoryName(revitFilePath), folderName);
        }

        if (date)
        {
            string formatedDate = DateTime.Today.ToString("yyyy-MM-dd");
            exportDirectory = Path.Combine(exportDirectory, formatedDate);
        }

        PathHelper.EnsureDirectory(exportDirectory);

        return exportDirectory;
    }

    public static void MoveAllFiles(string source, string destination)
    {
        PathHelper.EnsureDirectory(destination);

        DirectoryInfo directory = new(source);

        foreach (FileInfo info in directory.EnumerateFiles())
        {
            string path = Path.Combine(destination, info.Name);

            PathHelper.DeleteExistsFile(path);

            File.Move(info.FullName, path);
        }
    }

    public static void CreateZipFolder(string exportFolder, string exportDirectory)
    {
        string zipFilePath = $"{exportFolder}.zip";

        DirectoryInfo directory = new(exportFolder);

        SystemFolderOpener.OpenFolder(exportDirectory);

        Log.Debug("Start create Zip file by path {ZipFilePath}", zipFilePath);

        using ZipArchive archive = ZipFile.Open(zipFilePath, ZipArchiveMode.Create);

        foreach (FileInfo info in directory.EnumerateFiles())
        {
            if (FilePathHelper.IsFileAccessible(info))
            {
                try
                {
                    _ = archive.CreateEntryFromFile(info.FullName, info.Name);
                }
                catch (Exception ex)
                {
                    Log.Error("Failed: {FileName} {ErrorMessage}", info.Name, ex.Message);
                }

            }
        }
    }
}
