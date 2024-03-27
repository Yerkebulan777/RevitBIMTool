using Autodesk.Revit.DB;
using System.Diagnostics;


namespace RevitBIMTool.Utils;
public static class TransactionHelpers
{
    private static readonly object singleLocker = new object();

    public static void CreateTransaction(Document doc, string name, Action action)
    {
        lock (singleLocker)
        {
            using (Transaction trx = new Transaction(doc))
            {
                TransactionStatus status = trx.Start(name);
                if (status == TransactionStatus.Started)
                {
                    try
                    {
                        action?.Invoke();
                        status = trx.Commit();
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine(ex);
                        if (!trx.HasEnded())
                        {
                            status = trx.RollBack();
                        }
                    }
                }
            }
        }
    }


    public static void DeleteElements(Document doc, ICollection<ElementId> elemtIds)
    {
        lock (singleLocker)
        {
            using (Transaction trx = new Transaction(doc, "DeleteElements"))
            {
                IEnumerator<ElementId> enm = elemtIds.GetEnumerator();
                TransactionStatus status = trx.Start();
                if (status == TransactionStatus.Started)
                {
                    while (enm.MoveNext())
                    {
                        using (SubTransaction subtrx = new SubTransaction(doc))
                        {
                            try
                            {
                                status = subtrx.Start();
                                doc.Delete(enm.Current);
                                status = subtrx.Commit();
                            }
                            catch
                            {
                                status = subtrx.RollBack();
                            }
                        }
                    }

                    enm.Dispose();
                    elemtIds.Clear();
                    status = trx.Commit();
                }
            }
        }
    }



}