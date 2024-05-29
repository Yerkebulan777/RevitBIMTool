using Autodesk.Revit.UI;
using CommunicationService.Models;
using RevitBIMTool.Core;
using Serilog;
using System.Globalization;
using System.IO;


namespace RevitBIMTool;

[UsedImplicitly]
internal sealed class Application : IExternalApplication
{
    private RevitExternalEventHandler externalEventHandler;
    private static readonly string myDocumentsPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
    private static readonly string logerPath = Path.Combine(myDocumentsPath, "RevitBIMToolLog.txt");


    public Result OnStartup(UIControlledApplication application)
    {
        try
        {
            SetupUIPanel.Initialize(application);

            Log.Logger = new LoggerConfiguration()
                .WriteTo.Async(a => a.File(logerPath, rollOnFileSizeLimit: true))
                .MinimumLevel.Verbose()
                .CreateLogger();
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
                    Log.Information("Started Revit " + versionNumber);
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
}