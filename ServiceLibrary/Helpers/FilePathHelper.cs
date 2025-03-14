using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.AccessControl;
using System.Security.Permissions;
using System.Text;
using System.Text.RegularExpressions;
using Path = System.IO.Path;


namespace ServiceLibrary.Helpers
{
    public sealed class FilePathHelper
    {

        private static readonly string[] sectionAcronyms = { "AR", "AS", "APT", "KJ", "KR", "KG", "OV", "VK", "EOM", "EM", "PS", "SS", "OViK" };


        public static string GetUNCPath(string inputPath)
        {
            if (!string.IsNullOrEmpty(inputPath))
            {
                try
                {
                    using (RegistryKey key = Registry.CurrentUser.OpenSubKey("Network\\" + inputPath[0]))
                    {
                        if (key?.GetValue("RemotePath") is string rootPath && rootPath.Length > 0)
                        {
                            inputPath = Path.GetFullPath(rootPath + inputPath.Remove(0, 2));
                        }
                    }
                }
                catch (Exception ex)
                {
                    inputPath = ex.Message;
                }
            }

            return inputPath;
        }


        public static bool IsFileAccessible(string filePath)
        {
            FileInfo fileInfo = new FileInfo(filePath);

            if (fileInfo.Exists)
            {
                FileAttributes attributtes = fileInfo.Attributes;

                bool isHidden = attributtes.HasFlag(FileAttributes.Hidden);
                bool isSystem = attributtes.HasFlag(FileAttributes.System);
                bool isReadOnly = attributtes.HasFlag(FileAttributes.ReadOnly);

                if (!isHidden && !isSystem && !isReadOnly)
                {
                    try
                    {
                        // Check if there is access to read
                        FileIOPermission readPermission = new FileIOPermission(FileIOPermissionAccess.Read, filePath);
                        readPermission.Demand();

                        // Check if there is access to write
                        FileIOPermission writePermission = new FileIOPermission(FileIOPermissionAccess.Write, filePath);
                        writePermission.Demand();

                        // Проверяем права доступа
                        FileSecurity fileAcess = File.GetAccessControl(filePath);
                        Type securityType = typeof(System.Security.Principal.SecurityIdentifier);
                        AuthorizationRuleCollection rules = fileAcess.GetAccessRules(true, true, securityType);
                        return rules.Cast<FileSystemAccessRule>().Any(rule => rule.AccessControlType == AccessControlType.Allow);
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine(ex.Message);
                    }
                }
            }

            return false;
        }


        public static List<string> GetRvtPathsInDirectory(string inputPath, SearchOption option = SearchOption.TopDirectoryOnly)
        {
            const string seachRvtPattern = "*.rvt";
            const string containsFolderName = "RVT";

            List<string> output = new List<string>(100);

            Regex regexBackup = new Regex(@"\\.\\d\\d\\d\\d\\.rvt$", RegexOptions.Compiled);

            string[] collection = Directory.GetFiles(inputPath, seachRvtPattern, option);

            bool searchTopDirectoryOnly = option == SearchOption.TopDirectoryOnly;

            foreach (string filePath in collection)
            {
                string dirPath = Path.GetDirectoryName(filePath);

                if (searchTopDirectoryOnly)
                {
                    if (IsValidPath(filePath, ref regexBackup))
                    {
                        output.Add(filePath);
                    }
                }
                else if (dirPath.EndsWith(containsFolderName))
                {
                    if (IsValidPath(filePath, ref regexBackup))
                    {
                        output.Add(filePath);
                    }
                }

            }

            return output;
        }


        private static bool IsValidPath(string filePath, ref Regex regexBackup)
        {
            string fileName = Path.GetFileNameWithoutExtension(filePath);

            if (fileName.Length < 50)
            {
                if (!regexBackup.IsMatch(fileName))
                {
                    fileName = fileName.ToLower();

                    bool isCopy = fileName.Contains("копия");
                    bool isBuilding = fileName.Contains("строй");
                    bool isDetached = fileName.Contains("отсоединено");
                    bool isRecovery = fileName.Contains("восстановление");

                    if (!isRecovery && !isDetached && !isBuilding && !isCopy)
                    {
                        return IsValidFileSize(filePath);
                    }
                }
            }

            return false;
        }


        public static List<string> GetNWCFilePaths(string directory, out string output)
        {
            output = string.Empty;

            string seachNwcPattern = "*.nwc";
            string seachRvtPattern = "*.rvt";

            List<string> nwcPathList = new List<string>(100);
            List<string> rvtPathList = new List<string>(100);

            List<string> result = new List<string>(rvtPathList.Count);

            StringBuilder sb = new StringBuilder();

            if (Directory.Exists(directory))
            {
                SearchOption option = SearchOption.AllDirectories;
                SearchOption topOption = SearchOption.TopDirectoryOnly;

                string parentPath = Path.GetDirectoryName(directory);

                foreach (string subdir in Directory.EnumerateDirectories(parentPath, "*NWC", option))
                {
                    nwcPathList.AddRange(Directory.GetFiles(subdir, seachNwcPattern, topOption));
                }

                foreach (string subdir in Directory.EnumerateDirectories(parentPath, "*RVT", option))
                {
                    rvtPathList.AddRange(Directory.GetFiles(subdir, seachRvtPattern, topOption));
                }

                HashSet<string> rvtFileNameSet = new HashSet<string>(rvtPathList.Select(Path.GetFileNameWithoutExtension));

                _ = sb.AppendLine($"Total rvt files: {rvtPathList.Count}");
                _ = sb.AppendLine($"Total nwc files: {nwcPathList.Count}");
                _ = sb.AppendLine($"Base directory: {parentPath}");

                foreach (string nwcPath in nwcPathList)
                {
                    string fileName = Path.GetFileNameWithoutExtension(nwcPath);

                    if (fileName.Length < 50)
                    {
                        if (rvtFileNameSet.Remove(fileName))
                        {
                            _ = sb.AppendLine(fileName);
                            result.Add(nwcPath);
                        }
                    }
                    else
                    {
                        DeleteExistsFile(nwcPath);
                    }
                }
            }

            _ = sb.AppendLine($"Total {result.Count} files added");

            output = sb.ToString();

            return result;
        }


        private static bool IsValidFileSize(string filePath, int minSize = 30)
        {
            FileInfo fileInfo = new FileInfo(filePath);

            if (fileInfo.Exists)
            {
                int maxSizeInBytes = minSize * 1024 * 1024;
                return fileInfo.Length > maxSizeInBytes;
            }

            return false;
        }


        public static string GetPathFromRoot(string filePath, string searchName)
        {
            StringComparison compr = StringComparison.OrdinalIgnoreCase;

            DirectoryInfo dirInfo = new DirectoryInfo(filePath);

            if (dirInfo.Name.EndsWith(searchName, compr))
            {
                return dirInfo.FullName;
            }

            while (dirInfo != null)
            {
                dirInfo = dirInfo.Parent;

                if (dirInfo != null)
                {
                    string dirName = dirInfo.Name;

                    if (dirName.EndsWith(searchName, compr))
                    {
                        return dirInfo.FullName;
                    }
                }
            }

            return null;
        }


        public static List<string> GetProjectSectionPaths(string inputPath)
        {
            List<string> output = new List<string>();
            StringComparison comp = StringComparison.OrdinalIgnoreCase;
            DirectoryInfo inputDirectoryInfo = new DirectoryInfo(inputPath);
            foreach (DirectoryInfo dirInfo in inputDirectoryInfo.GetDirectories())
            {
                for (int idx = 0; idx < sectionAcronyms.Length; idx++)
                {
                    string section = sectionAcronyms[idx];

                    if (dirInfo.Name.EndsWith(section, comp))
                    {
                        output.Add(dirInfo.FullName);
                    }
                }
            }

            return output;
        }


        public static string GetProgectDirectory(string filePath)
        {
            return GetPathFromRoot(filePath, "PROJECT");
        }


        public static string GetProjectName(string filePath)
        {
            string projectPath = GetPathFromRoot(filePath, "PROJECT");

            if (!string.IsNullOrWhiteSpace(projectPath))
            {
                DirectoryInfo dirInfo = new DirectoryInfo(projectPath);

                DirectoryInfo parentDirInfo = dirInfo.Parent;

                if (parentDirInfo.Exists)
                {
                    return parentDirInfo.Name;
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


        public static void DeleteExistsFile(string filePath)
        {
            if (File.Exists(filePath))
            {
                try
                {
                    File.Delete(filePath);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error deleting file: {ex.Message}");
                }
            }
        }

    }

}
