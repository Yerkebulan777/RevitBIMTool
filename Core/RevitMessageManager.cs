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
        try
        {
            EndpointAddress endpoint = new(serviceUrlTcp);
            NetTcpBinding tspBinding = new(SecurityMode.Message);

            using ChannelFactory<IRevitHostService> factory = new(tspBinding);
            IRevitHostService proxy = factory.CreateChannel(endpoint);

            if (proxy is IClientChannel channel)
            {
                CloseIfFaultedChannel(channel);
                Log.Information($"Send message: {message}");
                proxy.SendMessageAsync(chatId, message).Wait();
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, ex.ToString());
        }
    }


    private static void CloseIfFaultedChannel(IClientChannel channel)
    {
        Log.Debug($"ClientChannel state: {channel.State}");

        if (channel.State == CommunicationState.Faulted)
        {
            channel.Abort();
        }
        else
        {
            channel.Close();
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
