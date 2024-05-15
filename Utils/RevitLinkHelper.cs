using Autodesk.Revit.DB;
using Serilog;
using System.Diagnostics;


namespace RevitBIMTool.Utils
{
    internal static class RevitLinkHelper
    {

        public static void CheckAndRemoveUnloadedLinks(Document doc)
        {
            Debug.WriteLine("Start checking and removing unloaded links ...");
            Log.Information("Start checking and removing unloaded links ...");
            FilteredElementCollector collector = new FilteredElementCollector(doc).OfClass(typeof(RevitLinkType));
            using Transaction trans = new(doc, "Check Links");
            Dictionary<string, RevitLinkType> linkNames = [];
            if (TransactionStatus.Started == trans.Start())
            {
                int count = collector.GetElementCount();
                Debug.WriteLine("All links counts: " + count);
                Log.Information("All links counts: " + count);
                foreach (RevitLinkType linkType in collector)
                {
                    string linkTypeName = linkType.Name;

                    if (!linkNames.ContainsKey(linkTypeName))
                    {
                        linkNames.Add(linkTypeName, linkType);
                        bool isLoaded = RevitLinkType.IsLoaded(doc, linkType.Id);
                        Debug.WriteLine($"Link: {linkTypeName} is loaded: {isLoaded}");
                        Log.Information($"Link: {linkTypeName} is loaded: {isLoaded}");

                        if (!isLoaded && linkType.AttachmentType == AttachmentType.Attachment)
                        {
                            TryReloadLink(linkType, linkTypeName);
                        }
                        else if (!isLoaded && linkType.AttachmentType != AttachmentType.Attachment)
                        {
                            TryDeleteLink(doc, linkType, linkTypeName);
                        }
                    }
                    else
                    {
                        TryDeleteLink(doc, linkType, linkTypeName);
                    }
                }
            }

            _ = trans.Commit();
        }


        private static void TryReloadLink(RevitLinkType linkType, string linkTypeName)
        {
            try
            {
                _ = linkType.Reload();
            }
            finally
            {
                Debug.WriteLine("Reload: " + linkTypeName);
                Log.Information("Reload: " + linkTypeName);
            }
        }


        private static void TryDeleteLink(Document doc, RevitLinkType linkType, string linkTypeName)
        {
            try
            {
                _ = doc.Delete(linkType.Id);
            }
            finally
            {
                Debug.WriteLine("Deleted: " + linkTypeName);
                Log.Information("Deleted: " + linkTypeName);
            }
        }


    }
}
