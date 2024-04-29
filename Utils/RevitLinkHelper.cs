using Autodesk.Revit.DB;
using System.Diagnostics;


namespace RevitBIMTool.Utils
{
    internal static class RevitLinkHelper
    {



        public static void CheckAndRemoveUnloadedLinks(Document doc)
        {
            FilteredElementCollector collector = new FilteredElementCollector(doc);
            collector = collector.OfClass(typeof(RevitLinkType)).WhereElementIsNotElementType();
            Debug.WriteLine("\nStart checking and removing unloaded links ...");
            using Transaction trans = new(doc, "Check Links");
            Dictionary<string, RevitLinkType> linkNames = [];
            if (TransactionStatus.Started == trans.Start())
            {
                foreach (RevitLinkType linkType in collector)
                {
                    if (!linkNames.ContainsKey(linkType.Name))
                    {
                        linkNames.Add(linkType.Name, linkType);

                        if (!RevitLinkType.IsLoaded(doc, linkType.Id))
                        {
                            if (linkType.AttachmentType == AttachmentType.Attachment)
                            {
                                try
                                {
                                    _ = linkType.Reload();
                                }
                                finally
                                {
                                    linkType.AttachmentType = AttachmentType.Attachment;
                                }
                            }
                            else
                            {
                                try
                                {
                                    _ = doc.Delete(linkType.Id);
                                }
                                catch (Exception ex)
                                {
                                    Debug.WriteLine("Error in processing link: {0}", ex);
                                }
                            }
                        }
                    }
                    else
                    {
                        try
                        {
                            _ = doc.Delete(linkType.Id);
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine(ex);
                        }
                    }
                }
            }

            _ = trans.Commit();
        }

    }
}
