using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Events;
using RevitBIMTool.Core;
using RevitBIMTool.Utils;
using Serilog;
using ServiceLibrary.Models;
using System.IO;


namespace RevitBIMTool;
internal sealed class Application : IExternalApplication
{
    private int length;
    private int counter;
    private string versionNumber;
    private RevitExternalEventHandler externalEventHandler;
    private static readonly string docPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);


    #region IExternalApplication

    public Result OnStartup(UIControlledApplication uiapp)
    {
        try
        {
            uiapp = uiapp ?? throw new ArgumentNullException(nameof(uiapp));
            versionNumber = uiapp.ControlledApplication.VersionNumber;
            SetupUIPanel.Initialize(uiapp);
            Log.Logger = ConfigureLogger();
        }
        catch (Exception ex)
        {
            System.Windows.Clipboard.SetText(ex.Message);
            uiapp.ControlledApplication.WriteJournalComment(ex.Message, true);
            return Result.Failed;
        }
        finally
        {
            if (TaskRequestContainer.Instance.DataAvailable(versionNumber, out length))
            {
                externalEventHandler = new RevitExternalEventHandler(versionNumber);

                if (ExternalEventRequest.Denied != externalEventHandler.Raise())
                {
                    uiapp.Idling += new EventHandler<IdlingEventArgs>(OnIdling);
                }
            }
        }

        return Result.Succeeded;
    }


    public Result OnShutdown(UIControlledApplication uiapp)
    {
        uiapp.Idling -= new EventHandler<IdlingEventArgs>(OnIdling);

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
                fileSizeLimitBytes: 100_000_000,
                retainedFileCountLimit: 5)
            .MinimumLevel.Debug()
            .CreateLogger();
    }

    #endregion


    #region IdlingEventHandler

    private void OnIdling(object sender, IdlingEventArgs e)
    {
        Log.Debug($"Idling session called {counter++}");

        TaskRequestContainer container = TaskRequestContainer.Instance;

        if (counter > length || !container.DataAvailable(versionNumber, out _))
        {
            RevitFileHelper.CloseRevitApplication();
        }

    }


    #endregion


}