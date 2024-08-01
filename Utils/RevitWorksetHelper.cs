using Autodesk.Revit.DB;
using Serilog;
using System.Text;
using View = Autodesk.Revit.DB.View;

namespace RevitBIMTool.Utils
{
    internal static class RevitWorksetHelper
    {

        public static void SetWorksetsToVisible(Document doc, View view)
        {
            if (doc.IsWorkshared)
            {
                using Transaction trans = new(doc);
                TransactionStatus status = trans.Start("SetWorksetsToVisible");
                IList<Workset> worksets = new FilteredWorksetCollector(doc).OfKind(WorksetKind.UserWorkset).ToWorksets();
                WorksetDefaultVisibilitySettings defaultVisibility = WorksetDefaultVisibilitySettings.GetWorksetDefaultVisibilitySettings(doc);

                try
                {
                    if (status == TransactionStatus.Started)
                    {
                        Log.Debug($"Set Worksets to Visible");
                        foreach (Workset workset in worksets)
                        {
                            if (workset.IsEditable)
                            {
                                WorksetId wid = new(workset.Id.IntegerValue);

                                if (!defaultVisibility.IsWorksetVisible(wid))
                                {
                                    defaultVisibility.SetWorksetVisibility(wid, true);
                                }

                                if (view.GetWorksetVisibility(wid) == WorksetVisibility.Hidden)
                                {
                                    view.SetWorksetVisibility(wid, WorksetVisibility.Visible);
                                }

                            }
                        }

                        status = trans.Commit();
                    }
                }
                catch (Exception ex)
                {
                    Log.Error(ex, ex.Message);
                }
                finally
                {
                    if (!trans.HasEnded())
                    {
                        _ = trans.RollBack();
                    }
                }
            }
        }


        public static void HideWorksetByNamePattern(Document doc, View view, string worksetPattern)
        {
            StringBuilder stringBuilder = new();

            IList<Workset> worksetList = new FilteredWorksetCollector(doc).OfKind(WorksetKind.UserWorkset).ToWorksets();
            worksetList = worksetList.Where(w => w.Name.Contains(worksetPattern)).ToList();

            using Transaction trans = new(doc, $"HideWorkset{worksetPattern}");
            TransactionStatus status = trans.Start();
            if (status == TransactionStatus.Started)
            {
                foreach (Workset workset in worksetList)
                {
                    try
                    {
                        WorksetId wid = new(workset.Id.IntegerValue);

                        _ = stringBuilder.AppendLine("Name: " + workset.Name);
                        _ = stringBuilder.AppendLine("Kind: " + workset.Kind);
                        _ = stringBuilder.AppendLine("Owner: " + workset.Owner);
                        _ = stringBuilder.AppendLine("Is open: " + workset.IsOpen);
                        _ = stringBuilder.AppendLine("UniqueId: " + workset.UniqueId);
                        _ = stringBuilder.AppendLine("Is editable: " + workset.IsEditable);
                        _ = stringBuilder.AppendLine("Is default: " + workset.IsDefaultWorkset);
                        _ = stringBuilder.AppendLine("Is visible: " + workset.IsVisibleByDefault);

                        if (view.GetWorksetVisibility(wid) == WorksetVisibility.Visible)
                        {
                            view.SetWorksetVisibility(wid, WorksetVisibility.Hidden);
                        }

                        Log.Debug($"WorksetsInfo: {stringBuilder}");

                        status = trans.Commit();
                    }
                    catch (Exception ex)
                    {
                        Log.Error(ex, ex.Message);
                    }
                    finally
                    {
                        if (!trans.HasEnded())
                        {
                            status = trans.RollBack();
                        }
                    }
                }
            }
        }
    }
}