using RevitBIMTool.Utils.Common;
using Serilog;
using System.Collections.Concurrent;
using System.IO;
using System.Xml;
using System.Xml.Serialization;

namespace RevitBIMTool.Utils.SystemHelpers;

public static class XmlHelper
{
    private const string MUTEX_PREFIX = "Global\\XmlMutexWorker_";

    private static readonly ConcurrentDictionary<Type, XmlSerializer> _serializerCache = new();

    /// <summary>
    /// Saves an object to XML file using file-specific mutex
    /// </summary>
    public static bool SaveToXml<T>(T xmlObject, string filePath) where T : class
    {
        if (xmlObject is null || string.IsNullOrWhiteSpace(filePath))
        {
            Log.Error("Invalid parameters: object or path is null");
            return false;
        }

        PathHelper.EnsureDirectory(Path.GetDirectoryName(filePath));
        string mutexId = MUTEX_PREFIX + filePath.GetHashCode();

        using Mutex mutex = new(false, mutexId);

        if (!mutex.WaitOne())
        {
            Log.Error("Failed to acquire mutex for file: {FilePath}", filePath);
            return false;
        }

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

            return File.Exists(filePath);
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

    /// <summary>
    /// Loads an object from XML file using file-specific mutex
    /// </summary>
    public static T LoadFromXml<T>(string filePath, T defaultValue = default) where T : class
    {
        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
        {
            Log.Debug("XML file unavailable: {FilePath}", filePath);
            return defaultValue;
        }

        string mutexId = MUTEX_PREFIX + filePath.GetHashCode();

        using Mutex mutex = new(false, mutexId);

        if (!mutex.WaitOne())
        {
            Log.Error("Failed to acquire mutex for file: {FilePath}", filePath);
            return defaultValue;
        }

        try
        {
            XmlSerializer serializer = _serializerCache.GetOrAdd(typeof(T), t => new XmlSerializer(t));

            using FileStream stream = new(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);

            if (stream.Length == 0)
            {
                Log.Warning("XML file is empty: {FilePath}", filePath);
                return defaultValue;
            }

            object deserializedObject = serializer.Deserialize(stream);

            T result = deserializedObject as T;

            if (result == null)
            {
                Log.Warning("Type mismatch: {ExpectedType}", typeof(T).Name);
            }

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
}
