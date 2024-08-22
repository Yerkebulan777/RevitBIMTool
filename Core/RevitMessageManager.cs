using Autodesk.Revit.UI;
using CommunicationService.Core;
using Serilog;
using System.ServiceModel;
using System.Windows;


namespace RevitBIMTool.Core;
public static class RevitMessageManager
{
    private const string serviceUrlTcp = "net.tcp://localhost:9000/RevitExternalService";


    public static void SendInfo(long chatId, string text)
    {
        try
        {
            Log.Information("Send: " + text);
            EndpointAddress endpoint = new(serviceUrlTcp);
            NetTcpBinding tspBinding = new(SecurityMode.Message);

            using (var factory = new ChannelFactory<IRevitHostService>(tspBinding))
            {
                IRevitHostService proxyService = factory.CreateChannel(endpoint);

                if (proxyService is IClientChannel channel)
                {
                    CloseIfFaultedChannel(channel);
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
