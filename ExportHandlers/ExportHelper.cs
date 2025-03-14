﻿using RevitBIMTool.Utils.Common;
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
                        archive.CreateEntryFromFile(info.FullName, info.Name);
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