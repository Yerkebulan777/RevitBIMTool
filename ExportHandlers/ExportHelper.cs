using RevitBIMTool.Utils;
using Serilog;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Security.Permissions;


namespace RevitBIMTool.ExportHandlers;
internal static class ExportHelper
{

    public static readonly string formatedDate = DateTime.Today.ToString("yyyy-MM-dd");


    public static string ExportDirectory(string revitFilePath, string folderName, bool folderDate = false)
    {
        if (string.IsNullOrEmpty(revitFilePath) || !File.Exists(revitFilePath))
        {
            throw new Exception("Revit file path cannot be null or empty");
        }

        string exportDirectory = RevitPathHelper.DetermineDirectory(revitFilePath, folderName);

        if (string.IsNullOrEmpty(exportDirectory))
        {
            exportDirectory = Path.Combine(Path.GetDirectoryName(revitFilePath), folderName);
        }

        if (folderDate)
        {
            exportDirectory = Path.Combine(exportDirectory, formatedDate);
        }

        RevitPathHelper.EnsureDirectory(exportDirectory);

        return exportDirectory;
    }


    public static bool IsTargetFileUpdated(string targetFilePath, string sourceFilePath, int minimum = 100)
    {
        if (File.Exists(targetFilePath) && File.Exists(sourceFilePath))
        {
            DateTime targetFileDate = File.GetLastWriteTime(targetFilePath);
            DateTime sourceFileDate = File.GetLastWriteTime(sourceFilePath);

            TimeSpan timeDifference = targetFileDate - sourceFileDate;
            long targetFileSize = new FileInfo(targetFilePath).Length;

            Log.Debug($"target last date: {targetFileDate:dd.MM.yyyy HH:mm}");
            Log.Debug($"source last date: {sourceFileDate:dd.MM.yyyy HH:mm}");

            bool isUpdated = timeDifference.TotalSeconds > minimum;
            bool isOutdated = timeDifference.TotalDays > minimum;

            bool isModifiedValid = isUpdated && !isOutdated;
            bool isFileSizeValid = targetFileSize > minimum;

            if (isModifiedValid && isFileSizeValid)
            {
                Log.Debug($"Target file valid");
                return true;
            }

            RevitPathHelper.DeleteExistsFile(targetFilePath);
        }

        return false;
    }


    public static string GetRelativePath(string fromPath, string toPath)
    {
        Uri fromUri = new(fromPath);
        Uri toUri = new(toPath);

        Uri relativeUri = fromUri.MakeRelativeUri(toUri);
        string relativePath = Uri.UnescapeDataString(relativeUri.ToString());

        return relativePath.Replace('/', Path.DirectorySeparatorChar);
    }


    public static void CreateZipTheFolder(string sourceDir, string targetDir)
    {
        string filename = Path.GetFileNameWithoutExtension(sourceDir);
        string destinationPath = Path.Combine(targetDir, filename + ".zip");

        if (Directory.Exists(sourceDir))
        {
            if (File.Exists(destinationPath))
            {
                File.Delete(destinationPath);
            }

            try
            {
                // Check if there is access to sourceDir
                FileIOPermission readPermission = new(FileIOPermissionAccess.Read, sourceDir);
                readPermission.Demand();

                // Check if there is access to targetDir
                FileIOPermission writePermission = new(FileIOPermissionAccess.Write, targetDir);
                writePermission.Demand();
            }
            catch (Exception ex)
            {
                throw new Exception($"No access to the folder: {ex.Message}");
            }
            finally
            {
                using ZipArchive archive = ZipFile.Open(destinationPath, ZipArchiveMode.Create);

                StringComparison comparison = StringComparison.OrdinalIgnoreCase;

                foreach (string filePath in Directory.GetFiles(sourceDir))
                {
                    FileInfo info = new(filePath);

                    if (info.Length > 0)
                    {
                        string entryName = info.Name;
                        string extension = info.Extension;

                        if (extension.EndsWith("dwg", comparison) || extension.EndsWith("jpg", comparison))
                        {
                            try
                            {
                                _ = archive.CreateEntryFromFile(filePath, entryName, CompressionLevel.Fastest);
                            }
                            catch (Exception ex)
                            {
                                Debug.WriteLine($"Failed: {entryName} {ex.Message}");
                            }
                        }
                    }
                }
            }
        }

    }


}