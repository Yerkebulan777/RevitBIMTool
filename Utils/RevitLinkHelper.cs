using Autodesk.Revit.DB;
using Serilog;
using System.Diagnostics;


namespace RevitBIMTool.Utils
{
    internal static class RevitLinkHelper
    {

        public static void CheckAndRemoveUnloadedLinks(Document doc)
        {
            FilteredElementCollector collector = new FilteredElementCollector(doc).OfClass(typeof(RevitLinkType));
            using Transaction trans = new(doc, "Check Links");
            Dictionary<string, RevitLinkType> linkNames = [];
            if (TransactionStatus.Started == trans.Start())
            {
                if (0 < collector.GetElementCount())
                {
                    foreach (ElementId id in collector.ToElementIds())
                    {
                        Element element = doc.GetElement(id);

                        if (element is RevitLinkType linkType)
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
                                    TryDeleteLink(doc, id, linkTypeName);
                                }
                            }
                            else
                            {
                                TryDeleteLink(doc, id, linkTypeName);
                            }
                        }
                    }

                    TransactionStatus status = trans.Commit();
                    Debug.WriteLine($"Transaction status: {status}");
                }
            }

        }


        private static void TryReloadLink(RevitLinkType linkType, string linkTypeName)
        {
            try
            {
                _ = linkType.Reload();
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Failed Reload: " + ex.Message);
                Log.Information("Failed Reload: " + ex.Message);
            }
            finally
            {
                Debug.WriteLine("Reload: " + linkTypeName);
                Log.Information("Reload: " + linkTypeName);
            }
        }


        private static void TryDeleteLink(Document doc, ElementId id, string linkTypeName)
        {
            try
            {
                _ = doc.Delete(id);
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Failed Delete: " + ex.Message);
                Log.Information("Failed Delete: " + ex.Message);
            }
            finally
            {
                Debug.WriteLine("Deleted: " + linkTypeName);
                Log.Information("Deleted: " + linkTypeName);
            }
        }



    }
}
