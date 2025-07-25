using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Events;
using RevitBIMTool.Core;
using RevitBIMTool.Utils.Common;
using Serilog;
using ServiceLibrary.Models;

namespace RevitBIMTool;

internal sealed class RevitBimToolApp : IExternalApplication
{
    public static string Version { get; set; }

    #region IExternalApplication

    public Result OnStartup(UIControlledApplication application)
    {
        try
        {
            application = application ?? throw new ArgumentNullException(nameof(application));
            Version = application.ControlledApplication.VersionNumber;
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
            if (TaskRequestContainer.Instance.ValidateData(Version, out _))
            {
                RevitExternalEventHandler externalEventHandler = new(Version);

                if (ExternalEventRequest.Denied != externalEventHandler.Raise())
                {
                    application.Idling += new EventHandler<IdlingEventArgs>(OnIdling);
                }
            }
        }

        return Result.Succeeded;
    }


    public Result OnShutdown(UIControlledApplication application)
    {
        application.Idling -= new EventHandler<IdlingEventArgs>(OnIdling);

        Log.CloseAndFlush();

        return Result.Succeeded;
    }

    #endregion


    #region IdlingEventHandler

    private void OnIdling(object sender, IdlingEventArgs e)
    {
        RevitFileHelper.CloseRevitApplication();
    }

    #endregion


}