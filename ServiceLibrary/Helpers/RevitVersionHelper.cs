using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;


namespace ServiceLibrary.Helpers
{
    public enum RevitVersion
    {
        Revit2019,
        Revit2020,
        Revit2021,
        Revit2022,
        Revit2023,
        Revit2024,
        Revit2025,
    }

    public static class RevitVersionHelper
    {
        private static Dictionary<RevitVersion, string> RevitInstallPaths { get; set; }


        #region Get Revit version from file

        private static object GetStorageRoot(string filePath)
        {
            Assembly windowsBaseAssembly = typeof(System.IO.Packaging.StorageInfo).Assembly;

            Type storageRootType = windowsBaseAssembly.GetType("System.IO.Packaging.StorageRoot", true, false);

            object storageRoot = storageRootType.InvokeMember("Open",
                BindingFlags.Static | BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.InvokeMethod,
                null, null, new object[] { filePath, FileMode.Open, FileAccess.Read, FileShare.Read }
            );

            return storageRoot;
        }


        private static Stream GetBasicFileInfoStream(object storageRoot)
        {
            MethodInfo getStreamInfoMethod = storageRoot.GetType().GetMethod("GetStreamInfo", new Type[] { typeof(string) });
            object streamInfo = getStreamInfoMethod.Invoke(storageRoot, new object[] { "BasicFileInfo" });
            MethodInfo getStreamMethod = streamInfo.GetType().GetMethod("GetStream", Type.EmptyTypes);
            return (Stream)getStreamMethod.Invoke(streamInfo, null);
        }


        private static byte[] ReadAllBytes(Stream stream)
        {
            using (MemoryStream memoryStream = new MemoryStream())
            {
                stream.CopyTo(memoryStream);
                return memoryStream.ToArray();
            }
        }


        private static byte[] GetBasicFileInfoBytes(string revitFilePath)
        {
            object storageRoot = GetStorageRoot(revitFilePath);
            Stream stream = GetBasicFileInfoStream(storageRoot);
            return ReadAllBytes(stream);
        }


        private static IList<int> FindAllIndicesOf(string text, string value)
        {
            List<int> indices = new List<int>();
            int index = text.IndexOf(value);
            while (index != -1)
            {
                indices.Add(index);
                index = text.IndexOf(value, index + 1);
            }
            return indices;
        }


        private static string GetRevitFileVersionInfoText(string revitFilePath)
        {
            string revitVersionInfoText = string.Empty;
            byte[] bytes = GetBasicFileInfoBytes(revitFilePath);
            string asciiString = Encoding.ASCII.GetString(bytes);
            IList<int> textMarkerIndices = FindAllIndicesOf(asciiString, "\r\n");

            if (textMarkerIndices.Count != 2)
            {
                textMarkerIndices = FindAllIndicesOf(asciiString, "\x04\r\x00\n\x00");
            }

            if (textMarkerIndices.Count == 2)
            {
                int startTextIndex = textMarkerIndices[0] + "\r\n".Length;
                byte[] textBytes = bytes.Skip(startTextIndex).Take(textMarkerIndices[1] - startTextIndex).ToArray();
                revitVersionInfoText = Encoding.Unicode.GetString(textBytes);
            }

            return revitVersionInfoText;
        }


        private static IList<string> ReadLinesFromText(string text)
        {
            List<string> lines = new List<string>();
            using (StringReader reader = new StringReader(text))
            {
                string line = string.Empty;
                while (line != null)
                {
                    line = reader.ReadLine();
                    if (!string.IsNullOrWhiteSpace(line))
                    {
                        lines.Add(line.Trim());
                    }
                }
            }
            return lines;
        }


        public static string GetRevitVersionText(string revitFilePath)
        {
            string result = null;

            IList<string> textInfoLines = ReadLinesFromText(GetRevitFileVersionInfoText(revitFilePath));
            result = textInfoLines.FirstOrDefault(line => line.StartsWith("Format:"));
            char[] characters = result?.ToCharArray();
            if (0 < characters.Length)
            {
                result = string.Join(string.Empty, characters.Where(character => char.IsDigit(character)));
            }

            return result;
        }

        #endregion


        #region Get Install Path Methods

        public static string GetRevitDirectory(string version)
        {
            if (string.IsNullOrEmpty(version)) { return null; }

            string registryKey = $@"SOFTWARE\Autodesk\Revit\{version}";

            using (RegistryKey skey = Registry.LocalMachine.OpenSubKey(registryKey))
            {
                Debug.Assert(skey != null, "64-битная операционная система");

                if (skey != null)
                {

                    string[] allSubKeys = skey.GetSubKeyNames();
                    string revitSubkey = allSubKeys.LastOrDefault(key => key.Contains("REVIT-"));
                    if (revitSubkey != null)
                    {
                        using (RegistryKey rvtKey = skey.OpenSubKey(revitSubkey))
                        {
                            object installLocation = rvtKey?.GetValue("InstallationLocation");
                            return installLocation?.ToString();
                        }
                    }
                }
            }

            registryKey = $@"SOFTWARE\Autodesk\Revit{version}";

            return registryKey;
        }


        private static string GetRevitInstallPath(RevitVersion revitVersion)
        {
            string version = Enum.GetName(typeof(RevitVersion), revitVersion)?.Remove(0, 5);
            return GetRevitDirectory(version);
        }


        public static void RevitExecutableFolderPaths()
        {
            foreach (string versionName in Enum.GetNames(typeof(RevitVersion)))
            {
                RevitVersion enumOfVersion = (RevitVersion)Enum.Parse(typeof(RevitVersion), versionName);
                string installLocation = GetRevitInstallPath(enumOfVersion);
                if (installLocation != null)
                {
                    RevitInstallPaths.Add(enumOfVersion, GetRevitInstallPath(enumOfVersion));
                }
            }
        }


        public static RevitVersion GetSupportedRevitVersion(string revitVersionNumber)
        {
            return RevitInstallPaths.Single(keyValue => keyValue.Value.Equals(revitVersionNumber)).Key;
        }

        #endregion


    }
}
