using Autodesk.Revit.UI;
using CommunicationService.Models;
using RevitBIMTool.Core;
using System.Globalization;


namespace RevitBIMTool;

[UsedImplicitly]
internal sealed class Application : IExternalApplication
{
    private RevitExternalEventHandler externalEventHandler;


    public Result OnStartup(UIControlledApplication application)
    {
        try
        {
            SetupUIPanel.Initialize(application);
        }
        catch (Exception ex)
        {
            application.ControlledApplication.WriteJournalComment(ex.Message, true);
            return Result.Failed;
        }
        finally
        {
            string versionNumber = application.ControlledApplication.VersionNumber;
            if (TaskRequestContainer.Instance.ValidateTaskData(versionNumber))
            {
                Thread.CurrentThread.CurrentCulture = CultureInfo.InvariantCulture;
                externalEventHandler = new RevitExternalEventHandler(versionNumber);
                if (ExternalEventRequest.Denied != externalEventHandler.Raise())
                {

                }
            }
        }

        return Result.Succeeded;
    }


    public Result OnShutdown(UIControlledApplication application)
    {
        return Result.Succeeded;
    }


}
