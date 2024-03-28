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
        bool result = !string.IsNullOrEmpty(exportDirectory);
        Debug.WriteLine(exportDirectory);

        if (result)
        {
            exportDirectory = folderDate
                ? Path.Combine(exportDirectory, formatedDate)
                : exportDirectory;

            RevitPathHelper.EnsureDirectory(exportDirectory);
        }

        return exportDirectory;
    }


    public static bool IsUpdatedFile(string targetPath, string sourcePath)
    {
        bool result = false;

        if (File.Exists(targetPath))
        {
            DateTime targetDate = File.GetLastWriteTime(targetPath);
            DateTime sourceDate = File.GetLastWriteTime(sourcePath);

            if (targetDate > sourceDate)
            {
                result = true;
            }
            else
            {
                try
                {
                    File.Delete(targetPath);
                }
                catch (IOException exc)
                {
                    Debug.WriteLine(exc.Message);
                }
            }
        }

        return result;
    }


    public static void ZipTheFolderWithSubfolders(string sourceFilePath, string directory)
    {
        string filename = Path.GetFileNameWithoutExtension(sourceFilePath);
        string destinationFilePath = Path.Combine(directory, filename + ".zip");

        RevitFileHelper.DeleteFileIfExists(destinationFilePath);

        using ZipArchive archive = ZipFile.Open(destinationFilePath, ZipArchiveMode.Create);

        foreach (string filePath in Directory.EnumerateFiles(sourceFilePath, "*.*", SearchOption.TopDirectoryOnly).Where(s => s.EndsWith(".dwg") || s.EndsWith(".jpg")))
        {
            if (File.Exists(filePath) && new FileInfo(filePath).Length > 0)
            {
                _ = archive.CreateEntryFromFile(filePath, Path.GetFileName(filePath));
            }
        }
    }



}