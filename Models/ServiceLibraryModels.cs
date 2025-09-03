using System;

namespace ServiceLibrary.Models
{
    public class TaskRequestContainer
    {
        private static TaskRequestContainer _instance;
        public static TaskRequestContainer Instance => _instance ??= new TaskRequestContainer();

        public bool ValidateData(string version, out string error)
        {
            error = null;
            return true;
        }
    }

    public class TaskRequest
    {
        public string Id { get; set; }
        public string Type { get; set; }
        public string Status { get; set; }
    }
}

namespace ServiceLibrary.Services
{
    public interface IExportService
    {
        void ExportToPdf(string fileName);
        void ExportToDwg(string fileName);
        void ExportToNwc(string fileName);
    }
}

namespace ServiceLibrary.Helpers
{
    public static class FileHelper
    {
        public static bool FileExists(string path) => File.Exists(path);
        public static void CreateDirectory(string path) => Directory.CreateDirectory(path);
    }
}