using Autodesk.Revit.DB;
using RevitBIMTool.Utils;
using Serilog;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Security.Permissions;
using System.Text.RegularExpressions;


namespace RevitBIMTool.ExportHandlers;
internal static class ExportHelper
{
    public static readonly string formatedDate = DateTime.Today.ToString("yyyy-MM-dd");


    public static string GetSheetNumber(ViewSheet sequenceSheet)
    {
        string stringNumber = sequenceSheet?.SheetNumber;

        if (!string.IsNullOrEmpty(stringNumber))
        {
            string invalidChars = new(Path.GetInvalidFileNameChars());
            string escapedInvalidChars = Regex.Escape(invalidChars);
            Regex regex = new($"(?<=\\d){escapedInvalidChars}");
            stringNumber = regex.Replace(stringNumber, ".");
        }

        return stringNumber.Trim();
    }


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


    public static bool IsTargetFileUpdated(string targetFilePath, string sourceFilePath)
    {
        if (File.Exists(targetFilePath) && File.Exists(sourceFilePath))
        {
            long targetFileSize = new FileInfo(targetFilePath).Length;

            DateTime targetFileDate = File.GetLastWriteTime(targetFilePath);
            DateTime sourceFileDate = File.GetLastWriteTime(sourceFilePath);

            Log.Information($"target last date: {targetFileDate:dd.MM.yyyy HH:mm}");
            Log.Information($"source last date: {sourceFileDate:dd.MM.yyyy HH:mm}");

            bool updated = targetFileSize > 0 && targetFileDate > sourceFileDate;

            if (!updated)
            {
                try
                {
                    File.Delete(targetFilePath);
                    Log.Information($"Deleted");
                }
                catch (IOException ex)
                {
                    Log.Error(ex, ex.Message);
                }
            }

            return updated;
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


    public static void ZipTheFolder(string sourceDir, string targetDir)
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