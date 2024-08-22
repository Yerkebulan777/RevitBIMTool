using Autodesk.Revit.UI;
using CommunicationService.Core;
using Serilog;
using System.ServiceModel;
using System.Windows;


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

            using (var factory = new ChannelFactory<IRevitHostService>(tspBinding))
            {
                IRevitHostService proxy = factory.CreateChannel(endpoint);

                if (proxy is IClientChannel channel)
                {
                    CloseIfFaultedChannel(channel);
                    proxy.SendMessageAsync(chatId, message);
                    Log.Information($"Send message: {message}");
                }
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, ex.ToString());
        }
        finally
        {
            Thread.Sleep(1000);
        }
    }


    private static void CloseIfFaultedChannel(IClientChannel channel)
    {
        if (channel.State == CommunicationState.Faulted)
        {
            channel.Abort();
        }
        else
        {
            try
            {
                channel.Close();
            }
            catch
            {
                channel.Abort();
            }
        }
    }


    public static void ShowInfo(string text)
    {
        if (!string.IsNullOrEmpty(text))
        {
            Log.Information(text);
            Clipboard.SetText(text);
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
