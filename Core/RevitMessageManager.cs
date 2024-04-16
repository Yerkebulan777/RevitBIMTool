using Autodesk.Revit.UI;
using CommunicationService.Core;
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
        EndpointAddress endpoint = new EndpointAddress(serviceUrlTcp);
        NetTcpBinding tspBinding = new NetTcpBinding(SecurityMode.Message);
        using (factory = new ChannelFactory<IRevitHostService>(tspBinding))
        {
            IRevitHostService proxyService = factory.CreateChannel(endpoint);

            if (proxyService is IClientChannel channel)
            {
                CloseIfFaultedChannel(channel);

                try
                {
                    await proxyService.SendMessageAsync(chatId, text);
                    await Task.Delay(5000);
                    channel.Dispose();
                }
                catch (Exception ex)
                {
                    ShowInfo(ex.Message);
                }
                finally
                {
                    await Task.Yield();
                }
            }
        }
    }


    private static void CloseIfFaultedChannel(IClientChannel channel)
    {
        if (channel.State == CommunicationState.Faulted)
        {
            try
            {
                channel.Close();
            }
            finally
            {
                channel.Abort();
            }
        }
    }


    public static void ShowInfo(string text)
    {
        if (!string.IsNullOrEmpty(text))
        {
            Debug.WriteLine(text);
            Clipboard.SetText(text);
            TaskDialog dialog = new TaskDialog("Revit")
            {
                MainContent = text,
                MainInstruction = "Information: ",
                MainIcon = TaskDialogIcon.TaskDialogIconInformation
            };
            _ = dialog.Show();
        }
    }


}
