using Autodesk.Revit.Attributes;
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

internal sealed class ExportPdfCommand : IExternalCommand, IExternalCommandAvailability
{
    public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
    {
        Thread.CurrentThread.CurrentCulture = CultureInfo.InvariantCulture;

        UIApplication uiapp = commandData.Application;
        UIDocument uidoc = uiapp.ActiveUIDocument;
        Document doc = uidoc.Document;

        try
        {
            LoggerHelper.SetupLogger(doc.Title);
            RevitLinkHelper.CheckAndRemoveUnloadedLinks(doc);
            string revitFilePath = PathHelper.GetRevitFilePath(doc);
            string outputDirectory = CommonExportManager.SetDirectory(revitFilePath, "03_PDF", true);
            ExportPdfProcessor.Execute(uidoc, revitFilePath, outputDirectory);
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
        return applicationData?.ActiveUIDocument.IsValidObject == true;
    }



}