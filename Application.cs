using Autodesk.Revit.UI;
using CommunicationService;
using RevitBIMTool.Core;


namespace RevitBIMTool;

[UsedImplicitly]
internal sealed class Application : IExternalApplication
{
    public string VersionNumber {  get; set; }
    private RevitExternalEventHandler externalEventHandler;
    private long botChatId;
    

    public Result OnStartup(UIControlledApplication application)
    {
        try
        {
            SetupUIPanel.Initialize(application);
            VersionNumber = application.ControlledApplication.VersionNumber;
        }
        catch (Exception ex)
        {
            application.ControlledApplication.WriteJournalComment(ex.Message, true);
            return Result.Failed;
        }
        finally
        {
            if (TaskRequestContainer.Instance.GetBotChatIdInData(ref botChatId))
            {
                externalEventHandler = new RevitExternalEventHandler(botChatId);
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
