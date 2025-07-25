﻿using Autodesk.Revit.DB;
using Serilog;
using System.Text;
using System.Text.RegularExpressions;
using View = Autodesk.Revit.DB.View;


namespace RevitBIMTool.Utils.Common;

internal static class RevitWorksetHelper
{
    public static void SetWorksetsToVisible(Document doc, View view)
    {
        if (doc.IsWorkshared)
        {
            using Transaction trx = new(doc);
            TransactionStatus status = trx.Start("SetWorksetsToVisible");
            IList<Workset> worksets = new FilteredWorksetCollector(doc).OfKind(WorksetKind.UserWorkset).ToWorksets();
            WorksetDefaultVisibilitySettings defaultVisibility = WorksetDefaultVisibilitySettings.GetWorksetDefaultVisibilitySettings(doc);

            try
            {
                if (status == TransactionStatus.Started)
                {
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

                    _ = trx.Commit();
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, ex.Message);
            }
            finally
            {
                Log.Debug($"Set worksets to visible");

                if (!trx.HasEnded())
                {
                    _ = trx.RollBack();
                }
            }
        }
    }

    public static void HideWorksetsByPattern(Document doc, View view, string pattern)
    {
        IList<Workset> worksetList = new FilteredWorksetCollector(doc).OfKind(WorksetKind.UserWorkset).ToWorksets();
        worksetList = [.. worksetList.Where(w => Regex.IsMatch(w.Name, pattern, RegexOptions.IgnoreCase))];

        if (worksetList.Count > 0)
        {
            StringBuilder builder = new();

            using Transaction trans = new(doc);

            TransactionStatus status = trans.Start($"HideWorkset{pattern}");

            _ = builder.AppendLine($"Start hide worksets by {pattern}");

            if (status == TransactionStatus.Started)
            {
                foreach (Workset workset in worksetList)
                {
                    using SubTransaction subTrans = new(doc);

                    try
                    {
                        _ = subTrans.Start();

                        WorksetId wid = new(workset.Id.IntegerValue);

                        _ = builder.AppendLine("PrinterName: " + workset.Name);
                        _ = builder.AppendLine("Kind: " + workset.Kind);
                        _ = builder.AppendLine("Is open: " + workset.IsOpen);
                        _ = builder.AppendLine("UniqueId: " + workset.UniqueId);
                        _ = builder.AppendLine("Is editable: " + workset.IsEditable);
                        _ = builder.AppendLine("Is default: " + workset.IsDefaultWorkset);
                        _ = builder.AppendLine("Is visible: " + workset.IsVisibleByDefault);

                        if (view.GetWorksetVisibility(wid) == WorksetVisibility.Visible)
                        {
                            view.SetWorksetVisibility(wid, WorksetVisibility.Hidden);
                        }

                        _ = subTrans.Commit();
                    }
                    catch (Exception ex)
                    {
                        _ = builder.AppendLine(ex.Message);
                        _ = subTrans.RollBack();
                    }
                    finally
                    {
                        Log.Debug(builder.ToString());
                    }
                }

                _ = trans.Commit();
            }
        }
    }
}
