using ServiceLibrary.Helpers;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;


namespace ServiceLibrary.Models
{
    public sealed class TaskRequestBuilder
    {
        public readonly int CommandNumber;
        public TaskRequestBuilder(int number)
        {
            CommandNumber = number;
        }


        public long ChatId { get; set; }
        public long UserId { get; set; }
        public int MessageId { get; set; }
        public TaskOption TaskOption { get; set; }
        public bool IsValidDirectory { get; set; }
        public string InputBasePath { get; set; }
        public bool IsProjectDirectory { get; set; }
        public List<string> OutputPaths { get; set; }
        public List<string> InputNames { get; set; }
        public List<string> InputPaths { get; set; }
        public List<int> InputIndices { get; set; }
        public string RevitVersion { get; set; }


        public void InputDirectory(string inputPath)
        {
            InputBasePath = inputPath;

            InputIndices = new List<int>(10);

            if (Directory.Exists(InputBasePath))
            {
                IsProjectDirectory = inputPath.EndsWith("PROJECT");

                InputPaths = IsProjectDirectory ? FilePathHelper.GetProjectSectionPaths(inputPath) : FilePathHelper.GetRvtPathsInDirectory(inputPath);

                InputNames = GetFileBaseNames(InputPaths);
            }
        }


        private List<string> GetFileBaseNames(List<string> filePaths)
        {
            List<string> fileNames = new List<string>(filePaths.Count);

            if (filePaths != null && filePaths.Count > 0)
            {
                IsValidDirectory = true;

                for (int idx = 0; idx < filePaths.Count; idx++)
                {
                    string filePath = filePaths[idx];
                    string name = Path.GetFileNameWithoutExtension(filePath);
                    fileNames.Add(name);
                }
            }

            return fileNames;
        }


        private List<string> GetRvtFilePathsByIndices(List<int> indices)
        {
            SearchOption option = SearchOption.AllDirectories;

            OutputPaths = new List<string>(indices.Count);

            for (int idx = 0; idx < indices.Count; idx++)
            {
                string path = InputPaths[indices[idx]];

                if (IsProjectDirectory)
                {
                    OutputPaths.AddRange(FilePathHelper.GetRvtPathsInDirectory(path, option));
                }
                else
                {
                    OutputPaths.Add(path);
                }
            }

            return OutputPaths;
        }


        public List<string> GetNavisworksCacheFiles(out string output)
        {
            List<string> inputRvtPaths = GetRvtFilePathsByIndices(InputIndices);

            List<string> outputNwcPaths = FilePathHelper.GetNWCFilePaths(InputBasePath, out output);

            Debug.WriteLineIf(inputRvtPaths.Count > 0, $"Input RVT files count: {inputRvtPaths.Count}");
            Debug.WriteLineIf(outputNwcPaths.Count > 0, $"Total NWC files count: {outputNwcPaths.Count}");

            HashSet<string> nameSet = new HashSet<string>(inputRvtPaths.Select(Path.GetFileNameWithoutExtension));

            for (int idx = 0; idx < outputNwcPaths.Count; idx++)
            {
                string tempName = Path.GetFileNameWithoutExtension(outputNwcPaths[idx]);

                if (!nameSet.Contains(tempName))
                {
                    outputNwcPaths.RemoveAt(idx);
                    idx--;
                }
            }

            return outputNwcPaths;
        }


        public void SubmitTaskRequests(out int fileCount)
        {
            List<TaskRequest> taskRequests = new List<TaskRequest>();

            OutputPaths = GetRvtFilePathsByIndices(InputIndices);

            fileCount = OutputPaths.Count;

            bool queue = IsProjectDirectory;

            for (int idx = 0; idx < fileCount; idx++)
            {
                string filePath = OutputPaths[idx];

                if (FilePathHelper.IsFileAccessible(filePath))
                {
                    RevitVersion = RevitVersionHelper.GetRevitVersionText(filePath);

                    TaskRequest request = new TaskRequest(UserId, ChatId, RevitVersion, CommandNumber, filePath);

                    if (0 != request.GetHashCode())
                    {
                        taskRequests.Add(request);
                    }
                }
            }

            if (TaskRequestContainer.Instance.SaveRangeTaskModelList(taskRequests, queue))
            {
                fileCount = taskRequests.Count;
            }

        }

    }

}
