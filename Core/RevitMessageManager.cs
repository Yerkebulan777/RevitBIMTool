using Autodesk.Revit.UI;
using CommunicationService.Core;
using Serilog;
using System.ServiceModel;


namespace RevitBIMTool.Core;
public static class RevitMessageManager
{

    private const string serviceUrlTcp = "net.tcp://localhost:9000/RevitExternalService";


    public static void SendInfo(long chatId, string message)
    {
        EndpointAddress endpoint = new(serviceUrlTcp);
        NetTcpBinding tspBinding = new(SecurityMode.Message);
        using ChannelFactory<IRevitHostService> client = new(tspBinding);

        try
        {
            Log.Information($"Start send message: {message}");

            IRevitHostService proxy = client.CreateChannel(endpoint);

            if (proxy is IClientChannel channel)
            {
                AbortIfFaulted(channel);
                proxy.SendMessageAsync(chatId, message).Wait();
                AbortIfFaulted(channel);
            }
        }
        catch (Exception ex)
        {
            client.Abort();
            Type excType = ex.GetType();
            Log.Error(ex, $"{excType.Name} : {ex.Message}");
        }
        finally
        {
            client.Close();
        }
    }


    private static void AbortIfFaulted(IClientChannel channel)
    {
        Log.Debug($"ClientChannel state: {channel.State}");

        if (channel.State == CommunicationState.Faulted)
        {
            channel.Abort();
        }
    }


    public static void ShowInfo(string text)
    {
        if (!string.IsNullOrEmpty(text))
        {
            Log.Information(text);
            TaskDialog dialog = new("Revit")
            {
                MainContent = text,
                MainInstruction = "Information: ",
                MainIcon = TaskDialogIcon.TaskDialogIconInformation
            };
            _ = dialog.Show();
        }
    }

}
