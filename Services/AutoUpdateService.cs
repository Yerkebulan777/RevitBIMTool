using System;
using System.Windows;
using AutoUpdaterDotNET;
using Serilog;

namespace RevitBIMTool.Services
{
    /// <summary>
    /// AutoUpdater service for checking and installing updates
    /// </summary>
    public static class AutoUpdateService
    {
        private static readonly string UpdateUrl = "https://api.github.com/repos/Yerkebulan777/RevitBIMTool/releases/latest";
        private static readonly string AppCastUrl = "https://raw.githubusercontent.com/Yerkebulan777/RevitBIMTool/main/updates/update.xml";

        /// <summary>
        /// Initialize AutoUpdater with configuration
        /// </summary>
        public static void Initialize()
        {
            try
            {
#if WINDOWS
                // Set basic AutoUpdater configuration
                AutoUpdater.ApplicationExitEvent += AutoUpdater_ApplicationExitEvent;
                AutoUpdater.CheckForUpdateEvent += AutoUpdater_CheckForUpdateEvent;
                
                // Configure updater behavior
                AutoUpdater.ShowSkipButton = false;
                AutoUpdater.ShowRemindLaterButton = true;
                AutoUpdater.RemindLaterTimeSpan = RemindLaterFormat.Days;
                AutoUpdater.RemindLaterAt = 7;
                
                // Optional: Set custom UI mode
                AutoUpdater.Mode = Mode.Normal;
                AutoUpdater.RunUpdateAsAdmin = false;
                
                Log.Information("AutoUpdater service initialized successfully");
#endif
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to initialize AutoUpdater service");
            }
        }

        /// <summary>
        /// Check for updates manually
        /// </summary>
        public static void CheckForUpdates()
        {
            try
            {
#if WINDOWS
                Log.Information("Checking for updates...");
                AutoUpdater.Start(AppCastUrl);
#endif
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error checking for updates");
            }
        }

        /// <summary>
        /// Check for updates automatically on startup
        /// </summary>
        public static void CheckForUpdatesAutomatic()
        {
            try
            {
#if WINDOWS
                // Set to check automatically
                AutoUpdater.LetUserSelectRemindLater = true;
                AutoUpdater.Mandatory = false;
                
                // Check for updates silently
                Log.Information("Automatic update check initiated");
                AutoUpdater.Start(AppCastUrl);
#endif
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error in automatic update check");
            }
        }

#if WINDOWS
        private static void AutoUpdater_CheckForUpdateEvent(UpdateInfoEventArgs args)
        {
            try
            {
                if (args.Error == null)
                {
                    if (args.IsUpdateAvailable)
                    {
                        Log.Information($"Update available: Version {args.CurrentVersion} -> {args.InstalledVersion}");
                        
                        // You can customize the update dialog here
                        if (MessageBox.Show($"Update available to version {args.CurrentVersion}. Would you like to update now?", 
                                          "Update Available", 
                                          MessageBoxButton.YesNo, 
                                          MessageBoxImage.Information) == MessageBoxResult.Yes)
                        {
                            AutoUpdater.DownloadUpdate(args);
                        }
                    }
                    else
                    {
                        Log.Information("No updates available");
                    }
                }
                else
                {
                    Log.Error(args.Error, "Error occurred while checking for updates");
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Exception in update check event handler");
            }
        }

        private static void AutoUpdater_ApplicationExitEvent()
        {
            try
            {
                Log.Information("Application exit requested by AutoUpdater");
                // Perform any cleanup here before application exits
                Application.Current?.Shutdown();
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error during application exit");
            }
        }
#endif
    }
}