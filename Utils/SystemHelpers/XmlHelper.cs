using RevitBIMTool.Utils.Common;
using Serilog;
using System.Collections.Concurrent;
using System.IO;
using System.Xml;
using System.Xml.Serialization;

namespace RevitBIMTool.Utils.SystemHelpers;

public static class XmlHelper
{
    private const string mutexId = "Global\\XmlMutexFileWorker";
    private static readonly ConcurrentDictionary<Type, XmlSerializer> _serializerCache = new();

    /// <summary>
    /// Saves an object to XML file using mutex synchronization
    /// </summary>
    public static bool SaveToXml<T>(T obj, string filePath) where T : class
    {
        if (obj == null)
        {
            Log.Error("Cannot save null object to XML");
            return false;
        }

        if (string.IsNullOrWhiteSpace(filePath))
        {
            Log.Error("File path cannot be empty");
            return false;
        }

        try
        {
            PathHelper.EnsureDirectory(Path.GetDirectoryName(filePath));
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Directory creation failed: {Message}", ex.Message);
            return false;
        }

        using Mutex mutex = new(false, mutexId, out _);
        if (!mutex.WaitOne(5000))
        {
            Log.Error("Failed to acquire mutex lock for XML file: {FilePath}", filePath);
            return false;
        }

        try
        {
            XmlSerializer serializer = GetOrCreateSerializer(typeof(T));

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

            if (!File.Exists(filePath))
            {
                Log.Error("File creation failed: {FilePath}", filePath);
                return false;
            }

            return true;
        }
        catch (UnauthorizedAccessException ex)
        {
            Log.Error(ex, "Access denied to file: {FilePath}", filePath);
            return false;
        }
        catch (IOException ex)
        {
            Log.Error(ex, "IO error writing file: {FilePath}", filePath);
            return false;
        }
        catch (InvalidOperationException ex)
        {
            Log.Error(ex, "Object serialization error: {Message}", ex.Message);
            return false;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Unexpected error saving XML: {Message}", ex.Message);
            return false;
        }
        finally
        {
            try
            {
                mutex.ReleaseMutex();
            }
            catch (Exception ex)
            {
                Log.Debug("Mutex release error: {Message}", ex.Message);
            }
        }
    }

    /// <summary>
    /// Loads an object from XML file using mutex synchronization
    /// </summary>
    public static T LoadFromXml<T>(string filePath, T defaultValue = default) where T : class
    {
        T result = defaultValue;

        if (string.IsNullOrWhiteSpace(filePath))
        {
            Log.Debug("XML file path not specified");
            return defaultValue;
        }

        using (Mutex mutex = new(false, mutexId, out _))
        {
            if (mutex.WaitOne(5000))
            {
                try
                {
                    XmlSerializer serializer = GetOrCreateSerializer(typeof(T));

                    using FileStream stream = new(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
                    if (stream.Length == 0)
                    {
                        Log.Warning("XML file is empty: {FilePath}", filePath);
                        return defaultValue;
                    }

                    object deserializedObject = serializer.Deserialize(stream);
                    result = deserializedObject as T;

                    if (result == null)
                    {
                        Log.Warning("Deserialized object type mismatch: {ExpectedType}", typeof(T).Name);
                    }
                }
                catch (InvalidOperationException ex)
                {
                    Log.Error(ex, "XML deserialization error: {Message}", ex.Message);
                }
                catch (XmlException ex)
                {
                    Log.Error(ex, "Invalid XML format: {Message}", ex.Message);
                }
                catch (IOException ex)
                {
                    Log.Error(ex, "File read error: {Message}", ex.Message);
                }
                catch (UnauthorizedAccessException ex)
                {
                    Log.Error(ex, "Access denied to file: {FilePath}", filePath);
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Unexpected error loading XML: {Message}", ex.Message);
                }
                finally
                {
                    try
                    {
                        mutex.ReleaseMutex();
                    }
                    catch (Exception ex)
                    {
                        Log.Debug("Mutex release error: {Message}", ex.Message);
                    }
                }
            }
        }

        return result;
    }

    /// <summary>
    /// Gets or creates an XmlSerializer for the specified type
    /// </summary>
    private static XmlSerializer GetOrCreateSerializer(Type type)
    {
        return _serializerCache.GetOrAdd(type, t => new XmlSerializer(t));
    }
}