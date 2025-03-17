using Serilog;
using System.Collections.Concurrent;
using System.IO;
using System.Xml;
using System.Xml.Serialization;

namespace RevitBIMTool.Utils.SystemHelpers;

public static class XmlHelper
{
    private const string PREFIX = "Global\\XmlMutexWorker_";

    private static readonly ConcurrentDictionary<Type, XmlSerializer> _serializerCache = new();

    /// <summary>
    /// Saves an object to XML file using file-specific mutex
    /// </summary>
    public static bool SaveToXml<T>(T xmlObject, string filePath) where T : class
    {
        using Mutex mutex = new(false, PREFIX + filePath.GetHashCode());

        if (mutex.WaitOne())
        {
            try
            {
                XmlSerializer serializer = _serializerCache.GetOrAdd(typeof(T), t => new XmlSerializer(t));

                XmlWriterSettings settings = new()
                {
                    Indent = true,
                    IndentChars = "  ",
                    Encoding = System.Text.Encoding.UTF8
                };

                using (XmlWriter writer = XmlWriter.Create(filePath, settings))
                {
                    serializer.Serialize(writer, xmlObject);
                }

                bool result = File.Exists(filePath);
                Log.Debug("XML file saved:", result);

                return result;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "XML save error: {Message}", ex.Message);
                return false;
            }
            finally
            {
                mutex.ReleaseMutex();
            }
        }

        Log.Error("Failed to acquire mutex!");
        return false;
    }


    /// <summary>
    /// Нормализует строку, удаляя пробелы и применяя CamelCase
    /// </summary>
    public static string NormalizeString(string input)
    {
        if (string.IsNullOrEmpty(input))
        {
            return string.Empty;
        }

        string normalized = input.Replace(" ", "");

        if (normalized.Length > 1)
        {
            normalized = char.ToUpperInvariant(normalized[0]) + normalized.Substring(1);
        }

        return normalized;
    }

    /// <summary>
    /// Loads an object from XML file using file-specific mutex
    /// </summary>
    public static T LoadFromXml<T>(string filePath, T defaultValue = default) where T : class
    {
        using Mutex mutex = new(false, PREFIX + filePath.GetHashCode());

        if (mutex.WaitOne())
        {
            try
            {
                XmlSerializer serializer = _serializerCache.GetOrAdd(typeof(T), t => new XmlSerializer(t));

                using FileStream stream = new(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);

                object deserializedObject = serializer.Deserialize(stream);

                T result = deserializedObject as T;

                return result;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "XML load error: {Message}", ex.Message);
                return defaultValue;
            }
            finally
            {
                mutex.ReleaseMutex();
            }
        }

        Log.Error("Failed to acquire mutex!");
        return defaultValue;
    }


}
