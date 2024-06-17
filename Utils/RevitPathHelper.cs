﻿using Autodesk.Revit.DB;
using Microsoft.Win32;
using Serilog;
using System.IO;
using System.Text.RegularExpressions;
using Path = System.IO.Path;


namespace RevitBIMTool.Utils;
public static class RevitPathHelper
{
    private static readonly string[] sectionAcronyms = { "AR", "AS", "KJ", "KR", "KG", "OV", "VK", "EOM", "EM", "PS", "SS", "OViK", "APT" };


    public static string GetUNCPath(string inputPath)
    {
        inputPath = Path.GetFullPath(inputPath);
        using (RegistryKey key = Registry.CurrentUser.OpenSubKey("Network\\" + inputPath[0]))
        {
            if (key != null)
            {
                inputPath = key.GetValue("RemotePath").ToString() + inputPath.Remove(0, 2).ToString();
            }
        }
        return inputPath;
    }


    public static List<string> GetRevitFilePaths(string inputpath)
    {
        string seachRvtPattern = $"*.rvt";
        List<string> output = new(10);
        SearchOption allFiles = SearchOption.AllDirectories;
        SearchOption topFiles = SearchOption.TopDirectoryOnly;
        string backupPattern = "\\.\\d\\d\\d\\d\\.(rvt|rfa)$";
        SearchOption option = inputpath.EndsWith("RVT") ? topFiles : allFiles;
        foreach (string filePath in Directory.GetFiles(inputpath, seachRvtPattern, option))
        {
            if (!Regex.IsMatch(filePath, backupPattern))
            {
                if (!filePath.Contains("отсоединено"))
                {
                    output.Add(filePath);
                }
            }
        }

        return output;
    }


    public static List<string> GetProjectSectionPaths(string inputpath)
    {
        List<string> output = [];
        DirectoryInfo inputDirectoryInfo = new(inputpath);
        foreach (DirectoryInfo dirInfo in inputDirectoryInfo.GetDirectories())
        {
            for (int idx = 0; idx < sectionAcronyms.Length; idx++)
            {
                if (dirInfo.Name.EndsWith(sectionAcronyms[idx]))
                {
                    output.Add(dirInfo.FullName);
                }
            }
        }

        return output;
    }


    public static string GetDirectoryFromRoot(string filepath, string searchName)
    {
        DirectoryInfo dirInfo = new(filepath);

        while (dirInfo != null)
        {
            dirInfo = dirInfo.Parent;
            if (dirInfo != null)
            {
                string dirName = dirInfo.Name;
                if (dirName.EndsWith(searchName))
                {
                    string dirPath = dirInfo.FullName;
                    if (Directory.Exists(dirPath))
                    {
                        return dirPath;
                    }
                }
            }
        }

        return null;
    }


    public static string GetSectionDirectoryPath(string filePath)
    {
        if (!string.IsNullOrEmpty(filePath))
        {
            foreach (string abbreviation in sectionAcronyms)
            {
                string path = GetDirectoryFromRoot(filePath, abbreviation);

                if (!string.IsNullOrEmpty(path))
                {
                    return path;
                }
            }
        }
        return null;
    }


    public static string DetermineDirectory(string filePath, string folderName)
    {
        string sectionDirectory = GetSectionDirectoryPath(filePath);

        if (Directory.Exists(sectionDirectory))
        {
            sectionDirectory = Path.Combine(sectionDirectory, folderName);
            EnsureDirectory(sectionDirectory);
        }

        return sectionDirectory;
    }


    public static string GetRevitFilePath(Document document)
    {
        if (document.IsWorkshared && !document.IsDetached)
        {
            ModelPath modelPath = document.GetWorksharingCentralModelPath();
            return ModelPathUtils.ConvertModelPathToUserVisiblePath(modelPath);
        }
        return document.PathName;
    }


    public static void ClearDirectory(string directoryPath)
    {
        if (Directory.Exists(directoryPath))
        {
            DirectoryInfo directory = new(directoryPath);
            foreach (FileInfo file in directory.EnumerateFiles())
            {
                try
                {
                    file.Delete();
                }
                catch (Exception ex)
                {
                    Log.Error(ex.Message);
                }
            }
        }
    }


    public static void DeleteExistsFile(string sheetFullPath)
    {
        if (File.Exists(sheetFullPath))
        {
            try
            {
                File.Delete(sheetFullPath);
            }
            catch (Exception ex)
            {
                Log.Error($"Error deleting file: {ex.Message}");
            }
        }
    }


    public static void EnsureDirectory(string directoryPath)
    {
        if (!Directory.Exists(directoryPath))
        {
            try
            {
                _ = Directory.CreateDirectory(directoryPath);
            }
            finally
            {
                Log.Debug($"Created directory: {directoryPath}");
            }
        }
    }


    public static void DeleteDirectory(string dirPath)
    {
        if (Directory.Exists(dirPath))
        {
            try
            {
                Directory.Delete(dirPath, true);
            }
            finally
            {
                Log.Debug($"Deleted directory: {dirPath}");
            }
        }
    }


    public static bool AwaitExistsFile(string filePath, int maxDuration = 100)
    {
        int counter = 0;

        while (counter < maxDuration)
        {
            counter++;
            Thread.Sleep(1000);
            if (File.Exists(filePath))
            {
                Log.Debug($"File found after {counter} sec");
                return true;
            }
        }

        return false;
    }


}
