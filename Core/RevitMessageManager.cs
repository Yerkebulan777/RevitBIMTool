using Autodesk.Revit.UI;
using Serilog;
using ServiceLibrary;
using System.ServiceModel;


namespace RevitBIMTool.Core;
public static class RevitMessageManager
{
    public static void SendInfo(long chatId, string message)
    {
        try
        {
            Uri baseAddress = new Uri($"net.tcp://localhost:9001/ ");

            EndpointAddress endpoint = new(baseAddress);
            NetTcpBinding tspBinding = new(SecurityMode.None);

            using ChannelFactory<IRevitService> client = new(tspBinding);

            IRevitService proxy = client.CreateChannel(endpoint);

            if (proxy is IClientChannel channel)
            {
                try
                {
                    proxy.SendMessageAsync(chatId, message).Wait();
                }
                catch (Exception ex)
                {
                    Log.Error(ex, $"Failed send message: {ex.Message}");
                }
                finally
                {
                    if (channel.State == CommunicationState.Faulted)
                    {
                        channel.Abort();
                    }
                    else
                    {
                        channel.Close();
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, $"{ex.Message} {ex.InnerException?.Message}");
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
