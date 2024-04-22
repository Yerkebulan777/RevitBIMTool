using Autodesk.Revit.UI;
using CommunicationService.Core;
using Serilog;
using System.Diagnostics;
using System.ServiceModel;
using System.Windows;


namespace RevitBIMTool.Core;
public static class RevitMessageManager
{
    private const string serviceUrlTcp = "net.tcp://localhost:9000/RevitExternalService";
    private static ChannelFactory<IRevitHostService> factory;

    public static async Task SendInfoAsync(long chatId, string text)
    {
        try
        {
            EndpointAddress endpoint = new(serviceUrlTcp);
            NetTcpBinding tspBinding = new(SecurityMode.Message);
            using (factory = new ChannelFactory<IRevitHostService>(tspBinding))
            {
                IRevitHostService proxyService = factory.CreateChannel(endpoint);

                if (proxyService is IClientChannel channel)
                {
                    CloseIfFaultedChannel(channel);
                    await proxyService.SendMessageAsync(chatId, text);
                    await Task.Delay(5000);
                    channel.Dispose();
                }
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, ex.ToString());
        }
        finally
        {
            await Task.Yield();
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
