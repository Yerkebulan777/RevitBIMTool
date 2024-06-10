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
    private static readonly string docPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
    private static readonly string logerPath = Path.Combine(docPath, "RevitBIMToolLog.txt");


    public Result OnStartup(UIControlledApplication application)
    {
        try
        {
            SetupUIPanel.Initialize(application);

            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Verbose()
                .WriteTo.File(logerPath)
                .CreateLogger();
        }
        catch (Exception ex)
        {
            System.Windows.Clipboard.SetText(ex.Message);
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
                    Log.Information($"Revit {versionNumber} handler started...");
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