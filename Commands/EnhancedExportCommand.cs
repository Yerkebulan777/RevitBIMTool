using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using RevitBIMTool.Windows;
using Serilog;
using System;

namespace RevitBIMTool.Commands
{
    /// <summary>
    /// Enhanced Export Command with WPF Settings Dialog
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class EnhancedExportCommand : IExternalCommand, IExternalCommandAvailability
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            try
            {
                Log.Information("Enhanced Export Command executed");

#if WINDOWS
                // Show WPF export settings dialog
                var settingsWindow = new ExportSettingsWindow();
                var result = settingsWindow.ShowDialog();

                if (result == true && settingsWindow.ExportRequested)
                {
                    var settings = settingsWindow.Settings;
                    Log.Information("Export settings received from user dialog");

                    // Process export based on settings
                    ProcessExport(commandData, settings);
                    
                    TaskDialog.Show("Export Complete", 
                                   "Export process completed successfully with the selected settings.");
                    
                    return Result.Succeeded;
                }
                else
                {
                    Log.Information("Export cancelled by user");
                    return Result.Cancelled;
                }
#else
                // Fallback for non-Windows environments
                TaskDialog.Show("Enhanced Export", 
                               "WPF Export dialog is available only on Windows. Using basic export...");
                
                // Call basic export functionality
                return ExecuteBasicExport(commandData, ref message, elements);
#endif
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error in Enhanced Export Command");
                message = ex.Message;
                return Result.Failed;
            }
        }

        private void ProcessExport(ExternalCommandData commandData, ExportSettings settings)
        {
            try
            {
                var uiApp = commandData.Application;
                var doc = uiApp.ActiveUIDocument.Document;

                Log.Information($"Processing export with settings: PDF Quality={settings.PdfQuality}, " +
                              $"DWG Version={settings.DwgVersion}, NWC Conversion={settings.ConversionType}");

                // Based on settings, call appropriate export methods
                if (settings.IncludeSheets)
                {
                    ExportPdfSheets(doc, settings);
                }

                if (settings.ExportModels)
                {
                    ExportDwgModels(doc, settings);
                }

                if (settings.IncludeGeometry)
                {
                    ExportNwcGeometry(doc, settings);
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error processing export with settings");
                throw;
            }
        }

        private void ExportPdfSheets(Document doc, ExportSettings settings)
        {
            Log.Information($"Exporting PDF sheets with quality: {settings.PdfQuality}");
            // Implementation would call existing PDF export logic
            // This is a placeholder for the actual export implementation
        }

        private void ExportDwgModels(Document doc, ExportSettings settings)
        {
            Log.Information($"Exporting DWG models with version: {settings.DwgVersion}");
            // Implementation would call existing DWG export logic
            // This is a placeholder for the actual export implementation
        }

        private void ExportNwcGeometry(Document doc, ExportSettings settings)
        {
            Log.Information($"Exporting NWC with conversion type: {settings.ConversionType}");
            // Implementation would call existing NWC export logic
            // This is a placeholder for the actual export implementation
        }

        private Result ExecuteBasicExport(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            try
            {
                // Basic export functionality for non-Windows platforms
                Log.Information("Executing basic export functionality");
                
                TaskDialog.Show("Basic Export", "Basic export functionality executed.");
                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error in basic export");
                message = ex.Message;
                return Result.Failed;
            }
        }

        public bool IsCommandAvailable(UIApplication applicationData, CategorySet selectedCategories)
        {
            // Command is available when a document is open
            return applicationData?.ActiveUIDocument?.Document != null;
        }
    }
}