using System;
using System.Windows;
using Serilog;

namespace RevitBIMTool.Windows
{
    /// <summary>
    /// Export Settings Window for configuring export options
    /// </summary>
    public partial class ExportSettingsWindow : Window
    {
        public ExportSettings Settings { get; private set; }
        public bool ExportRequested { get; private set; }

        public ExportSettingsWindow()
        {
#if WINDOWS
            InitializeComponent();
            InitializeSettings();
#endif
        }

        public ExportSettingsWindow(ExportSettings existingSettings) : this()
        {
            if (existingSettings != null)
            {
                LoadSettings(existingSettings);
            }
        }

        private void InitializeSettings()
        {
            try
            {
#if WINDOWS
                Settings = new ExportSettings();
                ExportRequested = false;
                
                // Set default values
                LoadDefaultSettings();
                
                Log.Information("Export Settings Window initialized");
#endif
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error initializing Export Settings Window");
            }
        }

        private void LoadDefaultSettings()
        {
#if WINDOWS
            // PDF defaults
            chkIncludeSheets.IsChecked = true;
            chkIncludeViews.IsChecked = false;
            chkCombineFiles.IsChecked = true;
            cmbPdfQuality.SelectedIndex = 1; // Medium
            cmbPaperSize.SelectedIndex = 0; // A4

            // DWG defaults  
            chkExportModels.IsChecked = true;
            chkExportSchedules.IsChecked = false;
            cmbDwgVersion.SelectedIndex = 2; // 2020
            cmbUnits.SelectedIndex = 0; // Millimeters

            // NWC defaults
            chkIncludeGeometry.IsChecked = true;
            chkIncludeProperties.IsChecked = true;
            chkIncludeTextures.IsChecked = false;
            cmbConversionType.SelectedIndex = 0; // Full
#endif
        }

        private void LoadSettings(ExportSettings settings)
        {
#if WINDOWS
            if (settings == null) return;

            // Load PDF settings
            chkIncludeSheets.IsChecked = settings.IncludeSheets;
            chkIncludeViews.IsChecked = settings.IncludeViews;
            chkCombineFiles.IsChecked = settings.CombineFiles;
            cmbPdfQuality.SelectedValue = settings.PdfQuality;
            cmbPaperSize.SelectedValue = settings.PaperSize;

            // Load DWG settings
            chkExportModels.IsChecked = settings.ExportModels;
            chkExportSchedules.IsChecked = settings.ExportSchedules;
            cmbDwgVersion.SelectedValue = settings.DwgVersion;
            cmbUnits.SelectedValue = settings.Units;

            // Load NWC settings
            chkIncludeGeometry.IsChecked = settings.IncludeGeometry;
            chkIncludeProperties.IsChecked = settings.IncludeProperties;
            chkIncludeTextures.IsChecked = settings.IncludeTextures;
            cmbConversionType.SelectedValue = settings.ConversionType;
#endif
        }

        private void BtnExport_Click(object sender, RoutedEventArgs e)
        {
            try
            {
#if WINDOWS
                // Collect settings from UI
                Settings = new ExportSettings
                {
                    // PDF settings
                    IncludeSheets = chkIncludeSheets.IsChecked ?? false,
                    IncludeViews = chkIncludeViews.IsChecked ?? false,
                    CombineFiles = chkCombineFiles.IsChecked ?? false,
                    PdfQuality = cmbPdfQuality.SelectedItem?.ToString() ?? "Medium",
                    PaperSize = cmbPaperSize.SelectedItem?.ToString() ?? "A4",

                    // DWG settings
                    ExportModels = chkExportModels.IsChecked ?? false,
                    ExportSchedules = chkExportSchedules.IsChecked ?? false,
                    DwgVersion = cmbDwgVersion.SelectedItem?.ToString() ?? "2020",
                    Units = cmbUnits.SelectedItem?.ToString() ?? "Millimeters",

                    // NWC settings
                    IncludeGeometry = chkIncludeGeometry.IsChecked ?? false,
                    IncludeProperties = chkIncludeProperties.IsChecked ?? false,
                    IncludeTextures = chkIncludeTextures.IsChecked ?? false,
                    ConversionType = cmbConversionType.SelectedItem?.ToString() ?? "Full"
                };

                ExportRequested = true;
                Log.Information("Export settings configured by user");
                this.DialogResult = true;
                this.Close();
#endif
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error processing export request");
                MessageBox.Show("Error processing export settings. Please try again.", "Error", 
                               MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                ExportRequested = false;
                Log.Information("Export cancelled by user");
                this.DialogResult = false;
                this.Close();
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error handling cancel request");
            }
        }
    }

    /// <summary>
    /// Export settings data class
    /// </summary>
    public class ExportSettings
    {
        // PDF settings
        public bool IncludeSheets { get; set; } = true;
        public bool IncludeViews { get; set; } = false;
        public bool CombineFiles { get; set; } = true;
        public string PdfQuality { get; set; } = "Medium";
        public string PaperSize { get; set; } = "A4";

        // DWG settings
        public bool ExportModels { get; set; } = true;
        public bool ExportSchedules { get; set; } = false;
        public string DwgVersion { get; set; } = "2020";
        public string Units { get; set; } = "Millimeters";

        // NWC settings
        public bool IncludeGeometry { get; set; } = true;
        public bool IncludeProperties { get; set; } = true;
        public bool IncludeTextures { get; set; } = false;
        public string ConversionType { get; set; } = "Full";
    }
}