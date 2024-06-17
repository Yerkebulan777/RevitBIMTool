using Autodesk.Revit.ApplicationServices;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using CommunicationService.Models;
using RevitBIMTool.Core;
using Serilog;
using System.Diagnostics;
using System.Globalization;
using System.IO;


namespace RevitBIMTool;

[UsedImplicitly]
internal sealed class Application : IExternalApplication
{
    private RevitExternalEventHandler externalEventHandler;
    private readonly Process process = Process.GetCurrentProcess();
    private readonly string docPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);


    #region IExternalApplication

    public Result OnStartup(UIControlledApplication application)
    {
        string versionNumber = application.ControlledApplication.VersionNumber;
        using Mutex mutex = new(true, $"Global\\Revit {versionNumber}");
        string logerPath = Path.Combine(docPath, $"RevitBIMTool.txt");

        if (mutex.WaitOne())
        {
            try
            {
                SetupUIPanel.Initialize(application);

                Log.Logger = new LoggerConfiguration()
                    .MinimumLevel.Verbose()
                    .WriteTo.File(logerPath, 
                    retainedFileCountLimit: 5, 
                    rollOnFileSizeLimit: true)
                    .CreateLogger();
            }
            catch (Exception ex)
            {
                System.Windows.Clipboard.SetText(ex.Message);
                application.ControlledApplication.WriteJournalComment(ex.Message, true);
                return Result.Failed;
            }
            finally
            {
                mutex.ReleaseMutex();

                if (TaskRequestContainer.Instance.ValidateTaskData(versionNumber))
                {
                    Thread.CurrentThread.CurrentCulture = CultureInfo.InvariantCulture;
                    externalEventHandler = new RevitExternalEventHandler(versionNumber);
                    if (ExternalEventRequest.Denied != externalEventHandler.Raise())
                    {
                        Log.Information($"Revit {versionNumber} handler started...");
                    }
                }
            }
        }

        return Result.Succeeded;
    }


    public Result OnShutdown(UIControlledApplication application)
    {
        Log.CloseAndFlush();
        return Result.Succeeded;
    }

    #endregion


    #region IExternalDBApplication

    public ExternalDBApplicationResult OnStartup(ControlledApplication application)
    {
        string versionNumber = application.VersionNumber;
        using Mutex mutex = new(true, $"Global\\Revit {versionNumber}");
        string logerPath = Path.Combine(docPath, $"RevitBIMTool.txt");

        if (mutex.WaitOne())
        {
            try
            {
                Log.Logger = new LoggerConfiguration()
                    .MinimumLevel.Verbose()
                    .WriteTo.File(logerPath,
                    retainedFileCountLimit: 5,
                    rollOnFileSizeLimit: true)
                    .CreateLogger();
            }
            catch (Exception ex)
            {
                System.Windows.Clipboard.SetText(ex.Message);
                application.WriteJournalComment(ex.Message, true);
                return ExternalDBApplicationResult.Failed;
            }
            finally
            {
                mutex.ReleaseMutex();

                if (TaskRequestContainer.Instance.ValidateTaskData(versionNumber))
                {
                    Thread.CurrentThread.CurrentCulture = CultureInfo.InvariantCulture;
                    externalEventHandler = new RevitExternalEventHandler(versionNumber);
                    if (ExternalEventRequest.Denied != externalEventHandler.Raise())
                    {
                        Log.Information($"Revit {versionNumber} handler started...");
                    }
                }
            }
        }

        return ExternalDBApplicationResult.Succeeded;
    }


    public ExternalDBApplicationResult OnShutdown(ControlledApplication application)
    {
        Log.CloseAndFlush();

        return ExternalDBApplicationResult.Succeeded;
    }

    #endregion


}