using Autodesk.Revit.DB;
using Serilog;
using System.Text;
using System.Text.RegularExpressions;
using View = Autodesk.Revit.DB.View;


namespace RevitBIMTool.Utils;

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
                    Log.Debug($"Set all worksets to Visible");
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


    public static void HideWorksetsByPattern(Document doc, View view, string pattern)
    {
        StringBuilder strBuilder = new();

        using Transaction trans = new(doc, $"HideWorkset{pattern}");
        IList<Workset> worksetList = new FilteredWorksetCollector(doc).OfKind(WorksetKind.UserWorkset).ToWorksets();
        worksetList = worksetList.Where(w => Regex.IsMatch(w.Name, pattern, RegexOptions.IgnoreCase)).ToList();

        if (worksetList.Count > 0)
        {
            Log.Debug($"Hide worksets {pattern}");
            TransactionStatus status = trans.Start();
            if (status == TransactionStatus.Started)
            {
                foreach (Workset workset in worksetList)
                {
                    using SubTransaction subTrans = new(doc);

                    try
                    {
                        status = subTrans.Start();

                        WorksetId wid = new(workset.Id.IntegerValue);

                        strBuilder.AppendLine("Name: " + workset.Name);
                        strBuilder.AppendLine("Kind: " + workset.Kind);
                        strBuilder.AppendLine("Is open: " + workset.IsOpen);
                        strBuilder.AppendLine("UniqueId: " + workset.UniqueId);
                        strBuilder.AppendLine("Is editable: " + workset.IsEditable);
                        strBuilder.AppendLine("Is default: " + workset.IsDefaultWorkset);
                        strBuilder.AppendLine("Is visible: " + workset.IsVisibleByDefault);

                        if (view.GetWorksetVisibility(wid) == WorksetVisibility.Visible)
                        {
                            view.SetWorksetVisibility(wid, WorksetVisibility.Hidden);
                        }

                        Log.Debug($"Hided workset: {strBuilder}");

                        status = subTrans.Commit();
                    }
                    catch (Exception ex)
                    {
                        status = subTrans.RollBack();
                        Log.Error(ex, ex.Message);
                    }
                    finally
                    {
                        strBuilder.Clear();
                    }
                }

                status = trans.Commit();
            }
        }
    }



}
