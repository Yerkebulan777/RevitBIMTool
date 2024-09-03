using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Events;
using RevitBIMTool.Core;
using RevitBIMTool.Utils;
using ServiceLibrary.Models;


namespace RevitBIMTool;
internal sealed class Application : IExternalApplication
{
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
        }
        catch (Exception ex)
        {
            System.Windows.Clipboard.SetText(ex.Message);
            uiapp.ControlledApplication.WriteJournalComment(ex.Message, true);
            return Result.Failed;
        }
        finally
        {
            if (TaskRequestContainer.Instance.DataAvailable(versionNumber, out int length))
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