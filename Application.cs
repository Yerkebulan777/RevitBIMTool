using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Events;
using RevitBIMTool.Core;
using RevitBIMTool.Utils;
using Serilog;
using ServiceLibrary.Models;


namespace RevitBIMTool;
internal sealed class Application : IExternalApplication
{
    private string versionNumber;
    private RevitExternalEventHandler externalEventHandler;


    #region IExternalApplication

    public Result OnStartup(UIControlledApplication uiapp)
    {
        try
        {
            uiapp = uiapp ?? throw new ArgumentNullException(nameof(uiapp));
            versionNumber = uiapp.ControlledApplication.VersionNumber;
            SetupUIPanel.Initialize(uiapp);
        }
        catch (Exception ex)
        {
            System.Windows.Clipboard.SetText(ex.Message);
            uiapp.ControlledApplication.WriteJournalComment(ex.Message, true);
            return Result.Failed;
        }
        finally
        {
            if (TaskRequestContainer.Instance.DataAvailable(versionNumber, out int count))
            {
                externalEventHandler = new RevitExternalEventHandler(versionNumber, count);

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


    #region IdlingEventHandler

    private void OnIdling(object sender, IdlingEventArgs e)
    {
        RevitFileHelper.CloseRevitApplication();
    }

    #endregion


}