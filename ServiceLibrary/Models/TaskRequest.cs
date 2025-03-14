using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;


namespace ServiceLibrary.Models
{

    public enum TaskOption
    {
        Standart,
        Advanced,
        Ultimate,
    }


    public sealed class TaskRequest
    {
        [JsonProperty]
        public long ChatId { get; internal set; }

        [JsonProperty]
        public long UserId { get; internal set; }

        [JsonProperty]
        public int CommandNumber { get; internal set; }

        [JsonProperty]
        public string RevitVersion { get; internal set; }

        [JsonProperty]
        public string RevitFilePath { get; internal set; }

        [JsonProperty]
        public string RevitFileName { get; internal set; }

        [JsonProperty]
        public int HashCode { get; internal set; }

        [JsonProperty]
        public string ExportFolder { get; set; }

        [JsonProperty]
        public string ExportBaseFile { get; set; }

        [JsonProperty]
        public DateTime CreatedTime { get; set; }


        [JsonConstructor]
        public TaskRequest(long chatId, long userId, string version, int number, string filePath)
        {
            ChatId = chatId;
            UserId = userId;
            RevitVersion = version;
            CommandNumber = number;
            RevitFilePath = filePath;
            CreatedTime = DateTime.Now;
            RevitFileName = Path.GetFileNameWithoutExtension(RevitFilePath);
        }


        public override int GetHashCode()
        {
            using (SHA256 sha256Hash = SHA256.Create())
            {
                string propetyText = $"{CommandNumber}{RevitFilePath}";
                byte[] bytes = sha256Hash.ComputeHash(Encoding.UTF8.GetBytes(propetyText));
                HashCode = BitConverter.ToInt32(bytes, 0);
                return HashCode;
            }
        }


    }

}
