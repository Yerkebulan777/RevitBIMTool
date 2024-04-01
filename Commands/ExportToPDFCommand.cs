﻿using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using RevitBIMTool.Core;
using RevitBIMTool.Utils;
using System.Globalization;


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
        Document document = uidoc.Document;

        try
        {
            string revitFilePath = RevitPathHelper.GetRevitFilePath(document);
            message = ExportToPDFHandler.ExportToPDF(document, revitFilePath);
        }
        catch (Exception ex)
        {
            TaskDialog.Show("Exception", "Exception: " + ex);
            return Result.Failed;
        }

        RevitMessageManager.ShowInfo(message);

        return Result.Succeeded;
    }


    public bool IsCommandAvailable(UIApplication applicationData, CategorySet selectedCategories)
    {
        UIDocument uidoc = applicationData?.ActiveUIDocument;
        return uidoc != null && uidoc.Document.IsDetached.Equals(false);
    }
}