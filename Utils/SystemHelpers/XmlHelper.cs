using RevitBIMTool.Utils.Common;
using Serilog;
using System.IO;
using System.Xml;
using System.Xml.Serialization;

namespace RevitBIMTool.Utils.SystemHelpers;

public static class XmlHelper
{
    private const string mutexId = "Global\\XmlMutexWorker";

    /// <summary>
    /// Сохраняет объект в XML файл с использованием Mutex
    /// </summary>
    public static void SaveToXml<T>(T obj, string filePath) where T : class
    {
        RevitPathHelper.EnsureDirectory(Path.GetDirectoryName(filePath));

        using (Mutex mutex = new(false, mutexId))
        {
            if (mutex.WaitOne(1000))
            {
                try
                {
                    XmlSerializer serializer = new(typeof(T));

                    XmlWriterSettings settings = new()
                    {
                        Indent = true,
                        IndentChars = "  ",
                        Encoding = System.Text.Encoding.UTF8
                    };

                    using (XmlWriter writer = XmlWriter.Create(filePath, settings))
                    {
                        serializer.Serialize(writer, obj);
                    }
                }
                catch (Exception ex)
                {
                    Log.Error(ex, ex.Message);
                }
                finally
                {
                    mutex.ReleaseMutex();
                    if (!File.Exists(filePath))
                    {
                        Log.Error($"State file creation failed: {filePath}");
                    }
                }
            }
        }
    }

    /// <summary>
    /// Загружает объект из XML файла с использованием Mutex
    /// </summary>
    public static T LoadFromXml<T>(string filePath, T defaultValue = default) where T : class
    {
        T result = defaultValue;

        if (!File.Exists(filePath))
        {
            Log.Debug($"XML файл не существует: {filePath}");
            return defaultValue;
        }

        using (Mutex mutex = new(false, mutexId))
        {
            if (mutex.WaitOne(5000))
            {
                try
                {
                    XmlSerializer serializer = new(typeof(T));

                    using (FileStream stream = new(filePath, FileMode.Open, FileAccess.Read, FileShare.Read))
                    {
                        result = serializer.Deserialize(stream) as T;
                    }
                }
                catch (Exception ex)
                {
                    Log.Error(ex, ex.Message);
                }
                finally
                {
                    mutex.ReleaseMutex();
                }
            }
        }

        return result;
    }
}