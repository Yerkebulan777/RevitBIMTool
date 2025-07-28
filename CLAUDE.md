# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

RevitBIMTool is a Revit add-in (.NET Framework 4.8) that provides automation tools for BIM workflows. The add-in creates a ribbon panel "Timas BIM Tool" with automation commands for exporting to PDF, DWG, and NWC formats.

## Multi-Version Revit Support

This project supports multiple Revit versions (2019, 2021, 2023) through configuration-based builds:
- Build configurations: `Debug R19`, `Debug R21`, `Debug R23`, `Release R19`, `Release R21`, `Release R23`
- Each configuration defines the `RevitVersion` property and corresponding preprocessor directives
- The Revit API NuGet package version is dynamically resolved based on `$(RevitVersion)`

## Build Commands

Build the solution for a specific Revit version:
```bash
dotnet build --configuration "Debug R23"
dotnet build --configuration "Release R21"
```

Build all configurations:
```bash
dotnet build
```

Clean build artifacts:
```bash
dotnet clean
```

## Project Structure

### Main Application
- `RevitBIMToolApp.cs` - Main application entry point implementing `IExternalApplication`
- `RevitBIMTool.addin` - Revit add-in manifest file

### Core Components
- `Core/SetupUIPanel.cs` - Creates the Revit ribbon interface with export commands
- `Core/RevitExternalEventHandler.cs` - Handles external events for automation
- `Core/RevitActionHandler.cs` - Processes automation actions
- `Core/MessageManager.cs` - Manages communication between components

### Commands
- `Commands/ExportPdfCommand.cs` - PDF export functionality
- `Commands/ExportDwgCommand.cs` - DWG export functionality  
- `Commands/ExportNwcCommand.cs` - NWC export functionality
- `Commands/AutomationCommand.cs` - General automation commands
- `Commands/TestCommand.cs` - Testing utilities

### Export Processing
- `ExportHandlers/` - Contains specialized processors for each export format
- `Utils/ExportPDF/` - PDF-specific utilities including printer management and merge handling
- `Utils/Common/` - Shared utilities for Revit operations (sheets, views, transactions, etc.)

### Database Integration
- `Database/` - Separate project handling PostgreSQL integration with Dapper ORM
- Supports both .NET Framework 4.8 and .NET 8.0 targeting

## Development Setup

The project includes automatic deployment to Revit's add-in directory:
- Build artifacts are automatically copied to `%AppData%\Autodesk\Revit\Addins\{RevitVersion}\`
- Clean operations remove deployed files

## Key Dependencies
- Revit API (version-specific via NuGet)
- iTextSharp 5.5.13.4 (PDF processing)
- Serilog.Sinks.File 6.0.0 (logging)
- Dapper 2.0.151 (database access)
- ServiceLibrary.dll (external automation service)

## Architecture Notes

- Uses external event handling pattern for thread-safe Revit API access
- Implements printer abstraction layer supporting multiple PDF printers (Adobe, BioPDF, ClawPDF, etc.)
- Includes comprehensive error handling and logging throughout
- Supports both interactive and automated operation modes
- Database layer is separated into its own project for modularity