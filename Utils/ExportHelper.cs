using Autodesk.Revit.DB;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
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


    public static void ZipTheFolderWithSubfolders(string sourceDir, string targetDir)
    {
        string filename = Path.GetFileNameWithoutExtension(sourceDir);
        string destinationPath = Path.Combine(targetDir, filename + ".zip");

        RevitFileHelper.DeleteFileIfExists(destinationPath);

        using ZipArchive archive = ZipFile.Open(destinationPath, ZipArchiveMode.Create);
        StringComparison comparison = StringComparison.OrdinalIgnoreCase;
        foreach (string filePath in Directory.EnumerateFiles(sourceDir))
        {
            try
            {
                string extension = Path.GetExtension(filePath);
                string entryName = GetRelativePath(sourceDir, filePath);
                
                if (extension.EndsWith("dwg", comparison) || extension.EndsWith("jpg", comparison))
                {
                    if (new FileInfo(filePath).Length > 0)
                    {
                        _ = archive.CreateEntryFromFile(filePath, entryName, CompressionLevel.Fastest);
                    }
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed convert to zip: '{filePath}': {ex.Message}");
            }
        }
    }



}