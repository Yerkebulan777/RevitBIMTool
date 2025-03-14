using Newtonsoft.Json;
using ServiceLibrary.Helpers;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;


namespace ServiceLibrary.Models
{
    public sealed partial class TaskRequestContainer
    {
        private const string mutexId = "Global\\{{{TaskContainerMutex}}}";
        private static readonly string documentsPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        private static readonly string jsonModelDataPath = Path.Combine(documentsPath, "TaskRequestModelData.json");
        private static ConcurrentDictionary<string, List<TaskRequest>> taskRequestModelData { get; set; }


        private static TaskRequestContainer _instance;
        public static TaskRequestContainer Instance
        {
            get
            {
                if (_instance == null)
                {
                    Interlocked.CompareExchange(ref _instance, new TaskRequestContainer(), null);
                    taskRequestModelData = new ConcurrentDictionary<string, List<TaskRequest>>();
                }

                return _instance;
            }
        }


        public bool ValidateData(string version, out int length)
        {
            int collectionCount = 0;

            if (string.IsNullOrEmpty(version))
            {
                throw new ArgumentNullException(nameof(version));
            }

            ConcurrentActionHandler.ExecuteWithMutex(mutexId, () =>
            {
                UpdateTaskDataFromFile();

                if (!taskRequestModelData.IsEmpty)
                {
                    if (taskRequestModelData.TryGetValue(version, out List<TaskRequest> collection))
                    {
                        Debug.WriteLine($"Collection TaskRequest length: {collection.Count}");
                        collectionCount = collection.Count;
                    }
                }

            });

            length = collectionCount;
            return collectionCount > 0;
        }


        public bool TryGetAvailable(out string version)
        {
            string localVersion = null;

            ConcurrentActionHandler.ExecuteWithMutex(mutexId, () =>
            {
                UpdateTaskDataFromFile();

                if (!taskRequestModelData.IsEmpty)
                {
                    var keyCollection = taskRequestModelData.Keys;
                    localVersion = keyCollection.FirstOrDefault();
                }

            });

            version = localVersion;
            return version != null;
        }


        public bool SaveRangeTaskModelList(List<TaskRequest> modelList, bool queue)
        {
            bool result = false;

            ConcurrentActionHandler.ExecuteWithMutex(mutexId, () =>
            {
                UpdateTaskDataFromFile();

                foreach (TaskRequest model in modelList)
                {
                    List<TaskRequest> collection = taskRequestModelData.GetOrAdd(model.RevitVersion, _ => new List<TaskRequest>(100));

                    if (queue)
                    {
                        collection.Add(model);
                    }
                    else
                    {
                        collection.Insert(0, model);
                    }
                }

                SaveTaskDataToFile();

                result = true;

            });

            return result;
        }


        public bool PopTaskModel(string version, out TaskRequest model)
        {
            bool result = false;

            TaskRequest firstModel = null;

            ConcurrentActionHandler.ExecuteWithMutex(mutexId, () =>
            {
                UpdateTaskDataFromFile();

                if (taskRequestModelData.TryGetValue(version, out List<TaskRequest> collection))
                {
                    firstModel = collection.FirstOrDefault();

                    if (firstModel != null && collection.Remove(firstModel))
                    {
                        SaveTaskDataToFile();
                        result = true;
                    }
                }
            });

            model = firstModel;

            return result;
        }


        private void SaveTaskDataToFile()
        {
            List<TaskRequest> allTasks = taskRequestModelData.SelectMany(pair => pair.Value).ToList();

            string jsonData = JsonConvert.SerializeObject(allTasks);

            File.WriteAllText(jsonModelDataPath, jsonData);
        }


        private void UpdateTaskDataFromFile()
        {
            if (File.Exists(jsonModelDataPath))
            {
                string jsonData = File.ReadAllText(jsonModelDataPath);

                List<TaskRequest> collection = JsonConvert.DeserializeObject<List<TaskRequest>>(jsonData);

                taskRequestModelData = new ConcurrentDictionary<string, List<TaskRequest>>();

                foreach (TaskRequest model in collection.Distinct(new TaskRequestComparer()))
                {
                    Debug.WriteLine($"Version: {model.RevitVersion}");
                    Debug.WriteLine($"Command: {model.CommandNumber}");
                    Debug.WriteLine($"RevitName: {model.RevitFileName}");

                    if (!taskRequestModelData.ContainsKey(model.RevitVersion))
                    {
                        taskRequestModelData[model.RevitVersion] = new List<TaskRequest>();
                    }

                    taskRequestModelData[model.RevitVersion].Add(model);
                }
            }
        }


    }
}
