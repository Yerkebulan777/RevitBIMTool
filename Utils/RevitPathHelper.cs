using Autodesk.Revit.DB;
using Microsoft.Win32;
using System.Diagnostics;
using System.IO;
using Path = System.IO.Path;


namespace RevitBIMTool.Utils;
public static class RevitPathHelper
{

    private static readonly string[] sectionAcronyms = { "AR", "AS", "APT", "KJ", "KR", "KG", "OV", "VK", "EOM", "EM", "PS", "SS", "OViK", "APT", "BIM" };


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


    private static string GetPathFromRoot(string filePath, string searchName)
    {
        StringComparison comparison = StringComparison.OrdinalIgnoreCase;

        DirectoryInfo dirInfo = new(filePath);

        while (dirInfo != null)
        {
            string dirName = dirInfo.Name;

            if (dirName.EndsWith(searchName, comparison))
            {
                return dirInfo.FullName;
            }
            else
            {
                dirInfo = dirInfo.Parent;
            }
        }

        return null;
    }


    public static string GetSectionName(string filePath)
    {
        foreach (string section in sectionAcronyms)
        {
            string tempPath = GetPathFromRoot(filePath, section);

            if (!string.IsNullOrEmpty(tempPath))
            {
                return section;
            }
        }

        return null;
    }


    public static string GetSectionDirectoryPath(string filePath)
    {
        foreach (string section in sectionAcronyms)
        {
            string tempPath = GetPathFromRoot(filePath, section);

            if (!string.IsNullOrEmpty(tempPath))
            {
                return tempPath;
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
                Debug.WriteLine($"Error deleting info: {ex.Message}");
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
                Debug.WriteLine($"Created logDir {directoryPath}");
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
            catch (Exception ex)
            {
                Debug.WriteLine($"Error deleting dir: {ex.Message}");
            }
        }
    }


    public static bool AwaitExistsFile(string filePath, int duration = 100)
    {
        int counter = 0;
        int totalWaitTime = 0;
        bool awaitResult = false;

        while (counter < duration)
        {
            lock (sectionAcronyms)
            {
                counter++;

                totalWaitTime += counter;
                Thread.Sleep(counter * 1000);

                if (File.Exists(filePath))
                {
                    awaitResult = true;
                    break;
                }
            }
        }

        Debug.WriteLine($"Total waiting: {totalWaitTime} seconds");

        return awaitResult;
    }


    public static void ClearDirectory(string directoryPath)
    {
        if (Directory.Exists(directoryPath))
        {
            DirectoryInfo directory = new(directoryPath);

            foreach (FileInfo info in directory.EnumerateFiles())
            {
                if (FileUnlockHelper.TryUnlockFile(info.FullName))
                {
                    try
                    {
                        info.Delete();
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine(ex.Message);
                    }
                }
            }
        }
    }


    public static void Move(string source, string destination)
    {
        EnsureDirectory(destination);

        ClearDirectory(destination);

        foreach (string file in Directory.GetFiles(source))
        {
            string fileName = Path.GetFileName(file);
            string path = Path.Combine(destination, fileName);

            File.Move(file, path);
        }
    }


}
