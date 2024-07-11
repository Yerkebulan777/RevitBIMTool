using Autodesk.Revit.DB;
using RevitBIMTool.Core;
using Serilog;
using System.Diagnostics;


namespace RevitBIMTool.Utils
{
    internal static class RevitLinkHelper
    {
        private static readonly object syncLocker = RevitExternalEventHandler.SyncLocker;


        public static void CheckAndRemoveUnloadedLinks(Document doc)
        {
            Log.Debug($"Start check links ...");
            FilteredElementCollector collector = new(doc);
            collector = collector.OfClass(typeof(RevitLinkType));
            using Transaction trans = new(doc, "CheckLinks");
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

                            lock (syncLocker)
                            {
                                if (!linkNames.ContainsKey(linkTypeName))
                                {
                                    linkNames.Add(linkTypeName, linkType);

                                    AttachmentType attachmentType = linkType.AttachmentType;

                                    bool isLoaded = RevitLinkType.IsLoaded(doc, linkType.Id);

                                    Log.Debug($"Link: {linkTypeName} is loaded: {isLoaded} ({attachmentType})");

                                    if (!isLoaded && attachmentType == AttachmentType.Overlay)
                                    {
                                        // Если тип наложение удалить
                                        TryDeleteLink(doc, id, linkTypeName);
                                    }
                                    else if (!isLoaded && attachmentType == AttachmentType.Attachment)
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
                _ = doc.Delete(id);
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
