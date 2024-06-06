using Autodesk.Revit.DB;
using Serilog;
using System.Diagnostics;


namespace RevitBIMTool.Utils
{
    internal static class RevitLinkHelper
    {
        public static void CheckAndRemoveUnloadedLinks(Document doc)
        {
            FilteredElementCollector collector = new(doc);
            collector = collector.OfClass(typeof(RevitLinkType));
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

                                Log.Debug($"Link: {linkTypeName} is loaded: {isLoaded}");

                                if (!isLoaded && linkType.AttachmentType == AttachmentType.Overlay)
                                {
                                    // Если тип наложение удалить
                                    TryDeleteLink(doc, id, linkTypeName);
                                }
                                else if (!isLoaded && linkType.AttachmentType == AttachmentType.Attachment)
                                {
                                    // Если тип прикрепление загрузить
                                    TryReloadLink(linkType, linkTypeName);
                                }
                            }
                            else
                            {
                                TryDeleteLink(doc, id, linkTypeName);
                            }
                        }
                    }

                    TransactionStatus status = trans.Commit();
                    Debug.WriteLine($"status: {status}");
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
                Log.Debug("Failed Reload: " + ex.Message);
            }
            finally
            {
                Log.Debug("Reload: " + linkTypeName);
            }
        }


        private static void TryDeleteLink(Document doc, ElementId id, string linkTypeName)
        {
            try
            {
                doc.Delete(id);
            }
            catch (Exception ex)
            {
                Log.Debug("Failed Delete: " + ex.Message);
            }
            finally
            {
                Log.Debug("Deleted: " + linkTypeName);
            }
        }



    }
}
