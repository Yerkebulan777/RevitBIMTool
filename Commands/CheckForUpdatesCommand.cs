using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using RevitBIMTool.Services;
using Serilog;
using System;

namespace RevitBIMTool.Commands
{
    /// <summary>
    /// Manual Update Check Command
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class CheckForUpdatesCommand : IExternalCommand, IExternalCommandAvailability
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            try
            {
                Log.Information("Manual update check initiated by user");

#if WINDOWS
                // Show manual update check
                AutoUpdateService.CheckForUpdates();
                
                return Result.Succeeded;
#else
                // Fallback for non-Windows environments
                TaskDialog.Show("Update Check", 
                               "Automatic updates are available only on Windows. " +
                               "Please check GitHub releases for the latest version:\n\n" +
                               "https://github.com/Yerkebulan777/RevitBIMTool/releases");
                
                return Result.Succeeded;
#endif
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error checking for updates");
                message = $"Error checking for updates: {ex.Message}";
                
                TaskDialog.Show("Update Check Error", 
                               "An error occurred while checking for updates. " +
                               "Please check your internet connection and try again.");
                
                return Result.Failed;
            }
        }

        public bool IsCommandAvailable(UIApplication applicationData, CategorySet selectedCategories)
        {
            // Command is always available
            return true;
        }
    }
}