﻿using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using RevitBIMTool.ExportHandlers;
using RevitBIMTool.Utils;
using RevitBIMTool.Utils.Common;
using System.Globalization;
using System.Windows;

namespace RevitBIMTool.Commands;

[Transaction(TransactionMode.Manual)]
[Regeneration(RegenerationOption.Manual)]
internal sealed class ExportNwcCommand : IExternalCommand, IExternalCommandAvailability
{
    public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
    {
        if (commandData.Application == null) { return Result.Cancelled; }
        Thread.CurrentThread.CurrentCulture = CultureInfo.InvariantCulture;

        UIApplication uiapp = commandData.Application;
        UIDocument uidoc = uiapp.ActiveUIDocument;
        Document doc = uidoc.Document;

        try
        {
            LoggerHelper.SetupLogger(doc.Title);
            RevitLinkHelper.CheckAndRemoveUnloadedLinks(doc);
            string revitFilePath = PathHelper.GetRevitFilePath(doc);
            string exportDirectory = CommonExportManager.SetDirectory(revitFilePath, "05_NWC", false);
            ExportNwcProcessor.Execute(uidoc, revitFilePath, exportDirectory);
        }
        catch (Exception ex)
        {
            _ = TaskDialog.Show("Exception", "Exception: \n" + ex);
            Clipboard.SetText(ex.ToString());
            return Result.Failed;
        }

        return Result.Succeeded;
    }


    public bool IsCommandAvailable(UIApplication applicationData, CategorySet selectedCategories)
    {
        return applicationData?.ActiveUIDocument.IsValidObject == true;
    }
}