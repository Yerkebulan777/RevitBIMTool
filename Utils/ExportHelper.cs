using Autodesk.Revit.DB;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Security.Permissions;
using System.Text.RegularExpressions;


namespace RevitBIMTool.Utils;
internal static class ExportHelper
{
    private static readonly Regex matchDigits = new(@"(\d+\.\d+|\d+)");
    public static readonly string formatedDate = DateTime.Today.ToString("yyyy-MM-dd");


    public static string GetSheetNumber(ViewSheet sequenceSheet)
    {
        string stringNumber = sequenceSheet?.SheetNumber.Trim();

        if (!string.IsNullOrEmpty(stringNumber))
        {
            stringNumber = StringHelper.ReplaceInvalidChars(stringNumber);
            MatchCollection matches = matchDigits.Matches(stringNumber);

            if (matches.Count > 0)
            {
                stringNumber = string.Join(string.Empty, matches.Cast<Match>().Select(m => m.Value));
            }
        }

        return stringNumber;
    }


    public static string ExportDirectory(string revitFilePath, string folderName, bool folderDate = false)
    {
        if (string.IsNullOrEmpty(revitFilePath) || !File.Exists(revitFilePath))
        {
            throw new Exception("Revit file path cannot be null or empty");
        }

        string exportDirectory = RevitPathHelper.DetermineDirectory(revitFilePath, folderName);

        if (!string.IsNullOrEmpty(exportDirectory))
        {
            if (folderDate)
            {
                exportDirectory = Path.Combine(exportDirectory, formatedDate);
            }

            RevitPathHelper.EnsureDirectory(exportDirectory);
        }

        return exportDirectory;
    }


    public static bool IsTargetFileUpdated(string targetFilePath, string sourceFilePath)
    {
        if (File.Exists(targetFilePath) && File.Exists(sourceFilePath))
        {
            DateTime targetFileDate = File.GetLastWriteTime(targetFilePath);
            DateTime sourceFileDate = File.GetLastWriteTime(sourceFilePath);

            bool result = targetFileDate > sourceFileDate;

            if (!result)
            {
                try
                {
                    File.Delete(targetFilePath);
                }
                catch (IOException exc)
                {
                    Debug.WriteLine(exc.Message);
                }
            }

            return result;
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
                // Проверяем, есть ли доступ к sourceDir
                FileIOPermission readPermission = new(FileIOPermissionAccess.Read, sourceDir);
                readPermission.Demand();

                // Проверяем, есть ли доступ к targetDir
                FileIOPermission writePermission = new(FileIOPermissionAccess.Write, targetDir);
                writePermission.Demand();

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
                            _ = archive.CreateEntryFromFile(filePath, entryName, CompressionLevel.Fastest);
                        }
                    }
                }
            }
            catch (UnauthorizedAccessException ex)
            {
                throw new Exception($"Нет доступа к директории: {ex.Message}");
            }
            catch (Exception ex)
            {
                throw new Exception($"Не удалось создать zip-архив: {ex.Message}");
            }
        }
        
    }



}