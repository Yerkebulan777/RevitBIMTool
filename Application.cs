using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Events;
using CommunicationService.Models;
using RevitBIMTool.Core;
using Serilog;
using System.Globalization;


namespace RevitBIMTool;

internal sealed class Application : IExternalApplication
{
    private RevitExternalEventHandler externalEventHandler;

    int counter = 0;

    #region IExternalApplication

    public Result OnStartup(UIControlledApplication application)
    {
        string versionNumber = application.ControlledApplication.VersionNumber;

        using Mutex mutex = new(true, $"Global\\Revit{versionNumber}");

        application.Idling += new EventHandler<IdlingEventArgs>(OnIdlingAsync);

        if (mutex.WaitOne(TimeSpan.FromSeconds(1000)))
        {
            try
            {
                SetupUIPanel.Initialize(application);
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


    private async void OnIdlingAsync(object sender, IdlingEventArgs e)
    {
        externalEventHandler = new RevitExternalEventHandler("");

        Log.Debug($"Idling session called");

        if (sender is UIApplication uiapp)
        {
            while (true)
            {
                counter++;

                await Task.Delay(1000);

                if (counter > 1000)
                {
                    externalEventHandler.CloseRevitApplication(uiapp);
                }

            }
        }
    }

}