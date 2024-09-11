﻿using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using RevitBIMTool.Core;
using RevitBIMTool.ExportHandlers;
using RevitBIMTool.Utils;
using System.Globalization;
using System.IO;
using System.Windows;


namespace RevitBIMTool.Commands;

[Transaction(TransactionMode.Manual)]
[Regeneration(RegenerationOption.Manual)]

internal sealed class ExportToPDFCommand : IExternalCommand, IExternalCommandAvailability
{
    public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
    {
        Thread.CurrentThread.CurrentCulture = CultureInfo.InvariantCulture;
        if (commandData.Application == null) { return Result.Cancelled; }

        UIApplication uiapp = commandData.Application;
        UIDocument uidoc = uiapp.ActiveUIDocument;
        Document doc = uidoc.Document;

        try
        {
            RevitLinkHelper.CheckAndRemoveUnloadedLinks(doc);
            string revitFilePath = RevitPathHelper.GetRevitFilePath(doc);
            string exportDirectory = ExportHelper.SetDirectory(revitFilePath, "03_PDF", true);
            ExportToPDFHandler.ExportExecute(uidoc, revitFilePath, exportDirectory);
        }
        catch (Exception ex)
        {
            TaskDialog.Show("Exception", "Exception: \n" + ex);
            Clipboard.SetText(ex.ToString());
            return Result.Failed;
        }

        return Result.Succeeded;
    }


    public bool IsCommandAvailable(UIApplication applicationData, CategorySet selectedCategories)
    {
        UIDocument uidoc = applicationData?.ActiveUIDocument;
        return uidoc != null && uidoc.Document.IsDetached.Equals(false);
    }
}