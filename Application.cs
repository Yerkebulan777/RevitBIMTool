using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Events;
using CommunicationService.Models;
using RevitBIMTool.Core;
using RevitBIMTool.Utils;
using Serilog;
using System.IO;


namespace RevitBIMTool;
internal sealed class Application : IExternalApplication
{
    private string versionNumber;
    private RevitExternalEventHandler externalEventHandler;
    private static readonly string docPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);


    #region IExternalApplication

    public Result OnStartup(UIControlledApplication application)
    {
        try
        {
            versionNumber = application.ControlledApplication.VersionNumber;
            SetupUIPanel.Initialize(application);
            Log.Logger = ConfigureLogger();
        }
        catch (Exception ex)
        {
            System.Windows.Clipboard.SetText(ex.Message);
            application.ControlledApplication.WriteJournalComment(ex.Message, true);
            return Result.Failed;
        }
        finally
        {
            if (TaskRequestContainer.Instance.ValidateTaskData(versionNumber))
            {
                application.Idling += new EventHandler<IdlingEventArgs>(OnIdling);

                externalEventHandler = new RevitExternalEventHandler(versionNumber);

                if (ExternalEventRequest.Denied != externalEventHandler.Raise())
                {
                    Log.Information($"Revit {versionNumber} started...");
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


    #region ConfigureLogger

    internal ILogger ConfigureLogger()
    {
        return new LoggerConfiguration()
            .WriteTo.File(Path.Combine(docPath, $"RevitBIMTool {versionNumber}.txt"),
                rollingInterval: RollingInterval.Infinite,
                fileSizeLimitBytes: 10_000_000,
                retainedFileCountLimit: 5)
            .MinimumLevel.Debug()
            .CreateLogger();
    }

    #endregion


    #region IdlingEventHandler

    private void OnIdling(object sender, IdlingEventArgs e)
    {
        Log.Debug($"Idling session called");
        Log.Debug($"Sender is {sender.GetType()}");
        if (sender is UIControlledApplication app)
        {
            app.Idling -= new EventHandler<IdlingEventArgs>(OnIdling);
            RevitFileHelper.CloseRevitApplication();
        }
    }

    #endregion

}