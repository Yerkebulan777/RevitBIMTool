# WPF and AutoUpdater.NET Integration

## Overview

This document describes the integration of WPF (Windows Presentation Foundation) for user interface and AutoUpdater.NET framework for automatic updates in the RevitBIMTool project.

## WPF Integration

### Features Added

1. **Enhanced Export Settings Dialog** (`Windows/ExportSettingsWindow.xaml`)
   - Tabbed interface for PDF, DWG, and NWC export settings
   - User-friendly controls for configuring export parameters
   - Validation and error handling

2. **Enhanced Export Command** (`Commands/EnhancedExportCommand.cs`)
   - Command that opens the WPF export settings dialog
   - Processes user selections and initiates appropriate exports
   - Fallback to basic functionality on non-Windows platforms

### Key Components

- **ExportSettingsWindow.xaml**: WPF window with tabbed interface
- **ExportSettingsWindow.xaml.cs**: Code-behind with business logic
- **ExportSettings**: Data class for export configuration
- **EnhancedExportCommand**: Revit command that uses the WPF dialog

### Conditional Compilation

WPF components use conditional compilation (`#if WINDOWS`) to ensure compatibility across platforms:

```csharp
#if WINDOWS
    // WPF-specific code
    var settingsWindow = new ExportSettingsWindow();
    var result = settingsWindow.ShowDialog();
#else
    // Fallback for non-Windows platforms
    TaskDialog.Show("Enhanced Export", "WPF Export dialog is available only on Windows.");
#endif
```

## AutoUpdater.NET Integration

### Features Added

1. **AutoUpdateService** (`Services/AutoUpdateService.cs`)
   - Centralized service for managing updates
   - Configurable update checking intervals
   - Custom update dialogs and user notifications

2. **Check Updates Command** (`Commands/CheckForUpdatesCommand.cs`)
   - Manual update check command available in Revit ribbon
   - User-initiated update checking

3. **Automatic Startup Check**
   - Updates are checked automatically 5 seconds after application startup
   - Non-intrusive background checking

### Configuration

- **Update URL**: Points to GitHub releases API
- **AppCast URL**: XML file containing update information
- **Update Behavior**:
  - Non-mandatory updates
  - 7-day reminder intervals
  - User can skip or postpone updates

### Update Process

1. Application checks for updates on startup (delayed)
2. If updates available, user is notified with options to:
   - Update now
   - Remind later (7 days)
   - Skip this version
3. Updates are downloaded and installed automatically when user consents

## Project Structure Changes

### New Directories
```
RevitBIMTool/
├── Services/           # AutoUpdater service
├── Windows/           # WPF windows and dialogs
├── Commands/          # Enhanced commands (existing + new)
└── updates/           # Update configuration files
```

### New Files
- `Services/AutoUpdateService.cs`
- `Windows/ExportSettingsWindow.xaml`
- `Windows/ExportSettingsWindow.xaml.cs`
- `Commands/EnhancedExportCommand.cs`
- `Commands/CheckForUpdatesCommand.cs`
- `updates/update.xml`

## Project File Updates

### Dependencies Added
```xml
<PackageReference Include="Autoupdater.NET.Official" Version="1.8.4" />
```

### Conditional WPF Support
```xml
<PropertyGroup Condition="'$(OS)' == 'Windows_NT'">
    <UseWPF>true</UseWPF>
    <UseWindowsForms>true</UseWindowsForms>
    <DefineConstants>WINDOWS</DefineConstants>
</PropertyGroup>
```

### WPF References
```xml
<ItemGroup Condition="'$(OS)' == 'Windows_NT'">
    <Reference Include="PresentationCore" />
    <Reference Include="PresentationFramework" />
    <Reference Include="WindowsBase" />
    <Reference Include="System.Xaml" />
</ItemGroup>
```

## Usage

### For End Users

1. **Enhanced Export**:
   - Click "Enhanced Export" button in Revit ribbon
   - Configure export settings in the dialog
   - Click "Export" to process with selected settings

2. **Manual Update Check**:
   - Click "Check Updates" button in Revit ribbon
   - Follow prompts if updates are available

3. **Automatic Updates**:
   - Updates are checked automatically on startup
   - User will be notified if updates are available

### For Developers

1. **Adding New WPF Windows**:
   ```csharp
   #if WINDOWS
   var myWindow = new MyWindow();
   var result = myWindow.ShowDialog();
   #endif
   ```

2. **Customizing Update Behavior**:
   - Modify `AutoUpdateService.cs` configuration
   - Update `updates/update.xml` for new releases

3. **Cross-Platform Compatibility**:
   - Use conditional compilation for WPF code
   - Provide fallbacks for non-Windows platforms

## Build Considerations

- **Windows**: Full WPF and AutoUpdater functionality
- **Linux/Mac**: Conditional compilation excludes WPF, provides fallbacks
- **Cross-Platform**: Project builds successfully on all platforms

## Future Enhancements

1. **Additional WPF Dialogs**:
   - Settings/preferences window
   - Progress dialogs for long operations
   - Help and about dialogs

2. **Enhanced Update Features**:
   - Delta updates for smaller downloads
   - Update rollback capability
   - Custom update server support

3. **UI Improvements**:
   - Custom themes and styling
   - Accessibility features
   - Localization support

## Troubleshooting

### Common Issues

1. **WPF Not Available**: Ensure running on Windows with .NET Framework 4.8+
2. **Update Check Fails**: Verify internet connection and GitHub access
3. **XAML Build Errors**: Ensure proper conditional compilation setup

### Logging

All WPF and AutoUpdater operations are logged using Serilog:
- Check log files for detailed error information
- Log level can be adjusted in configuration