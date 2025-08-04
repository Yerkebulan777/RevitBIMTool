using Autodesk.Revit.DB;
using Microsoft.Win32;
using System.Diagnostics;

namespace CommonUtils
{
    public static class PathHelper
    {
        private static readonly string[] SectionAcronyms = { "AR", "AS", "APT", "KJ", "KR", "KG", "OV", "VK", "EOM", "EM", "PS", "SS", "OViK", "APT", "BIM" };


        public static string GetProjectDirectory(Document doc, out string revitFilePath)
        {
            revitFilePath = GetRevitFilePath(doc);
            return LocateDirectory(revitFilePath, "*PROJECT*");
        }


        public static string GetExportDirectory(Document doc, out string revitFilePath, string folderName)
        {
            revitFilePath = GetRevitFilePath(doc);
            return DetermineDirectory(revitFilePath, folderName);
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

        /// <summary>
        /// Находит родительскую папку проекта по заданному пути
        /// </summary>
        static string LocateDirectory(string filePath, string pattern)
        {
            SearchOption opt = SearchOption.TopDirectoryOnly;
            DirectoryInfo dirInfo = Directory.GetParent(filePath);
            return dirInfo.FindFolder(pattern, opt) ?? dirInfo.FullName;
        }

        /// <summary>
        /// Найти папку по маске
        /// </summary>
        private static string FindFolder(this DirectoryInfo dirInfo, string pattern, SearchOption opt)
        {
            return Directory.GetDirectories(dirInfo.FullName, pattern, opt).FirstOrDefault();
        }

        /// <summary>
        /// Получает имя секции по пути к файлу
        /// </summary>
        public static string GetSectionName(string filePath)
        {
            foreach (string section in SectionAcronyms)
            {
                string tempPath = LocateDirectory(filePath, section);

                if (!string.IsNullOrEmpty(tempPath))
                {
                    return section;
                }
            }

            throw new InvalidOperationException("Section not found!");
        }

        /// <summary>
        /// Определяет директорию для сохранения файлов в секции
        /// </summary>
        public static string DetermineDirectory(string filePath, string folderName)
        {
            string sectionPath = LocateSectionDirectory(filePath);
            string targetDirectory = Path.Combine(sectionPath, folderName);
            EnsureDirectory(Path.Combine(targetDirectory, folderName));
            return targetDirectory;
        }

        /// <summary>
        /// Определяет директорию секции по пути к файлу
        /// </summary>
        private static string LocateSectionDirectory(string filePath)
        {
            foreach (string section in SectionAcronyms)
            {
                string tempPath = LocateDirectory(filePath, section);

                if (!string.IsNullOrEmpty(tempPath))
                {
                    return tempPath;
                }
            }

            DirectoryInfo dirInfo = Directory.GetParent(filePath);
            return Path.GetDirectoryName(dirInfo.Parent.FullName);
        }

        /// <summary>
        /// Получает UNC путь для сетевого ресурса
        /// </summary>
        public static string GetUNCPath(string inputPath)
        {
            inputPath = Path.GetFullPath(inputPath);

            using (RegistryKey key = Registry.CurrentUser.OpenSubKey("Network\\" + inputPath[0]))
            {
                if (key != null)
                {
                    inputPath = key.GetValue("RemotePath").ToString() + inputPath[2..].ToString();
                }
            }

            return inputPath;
        }

        /// <summary>
        /// Создание директории, если не существует
        /// </summary>
        public static void EnsureDirectory(string filePath)
        {
            if (!string.IsNullOrEmpty(filePath) && !Directory.Exists(filePath))
            {
                _ = Directory.CreateDirectory(filePath);
            }
        }

        /// <summary>
        /// Удаляет существующий файл по указанному пути
        /// </summary>
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
                    Debug.Fail($"Error deleting info: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Перемещает все файлы из одной директории в другую
        /// </summary>
        public static void MoveAllFiles(string source, string destination)
        {
            EnsureDirectory(destination);
            DirectoryInfo directory = new(source);

            foreach (FileInfo info in directory.EnumerateFiles())
            {
                string path = Path.Combine(destination, info.Name);
                File.Move(info.FullName, path);
                DeleteExistsFile(path);
            }
        }

        /// <summary>
        /// Ожидает существование файла в течение заданного времени
        /// </summary>
        public static bool AwaitExistsFile(string filePath, int duration = 300)
        {
            int counter = 0;

            while (counter < duration)
            {
                lock (SectionAcronyms)
                {
                    counter++;

                    Thread.Sleep(1000);

                    if (File.Exists(filePath))
                    {
                        Debug.Print($"Waiting: {counter} seconds");
                        return true;
                    }
                }
            }

            return false;
        }


    }
}
