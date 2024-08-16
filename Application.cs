using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Events;
using CommunicationService.Models;
using RevitBIMTool.Core;
using Serilog;
using System.Globalization;
using System.IO;


namespace RevitBIMTool;

internal sealed class Application : IExternalApplication
{
    private RevitExternalEventHandler externalEventHandler;
    private static readonly string docPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);


    #region IExternalApplication

    public Result OnStartup(UIControlledApplication application)
    {
        string versionNumber = application.ControlledApplication.VersionNumber;

        using Mutex mutex = new(true, $"Global\\Revit{versionNumber}");

        if (mutex.WaitOne(TimeSpan.FromSeconds(1000)))
        {
            try
            {
                SetupUIPanel.Initialize(application);
                Log.Logger = ConfigureLogger(versionNumber);
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
                        Log.Information($"Revit {versionNumber} started...");
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


    #region Logger

    internal ILogger ConfigureLogger(string versionNumber)
    {
        return new LoggerConfiguration()
            .WriteTo.File(Path.Combine(docPath, $"RevitBIMTool {versionNumber}.txt"),
                rollingInterval: RollingInterval.Day,
                fileSizeLimitBytes: 10_000_000,
                retainedFileCountLimit: 3)
            .MinimumLevel.Debug()
            .CreateLogger();
    }

    #endregion


}