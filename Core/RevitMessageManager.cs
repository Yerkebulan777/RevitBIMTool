using Autodesk.Revit.UI;
using Serilog;
using ServiceLibrary;
using System.ServiceModel;
using System.ServiceModel.Channels;


namespace RevitBIMTool.Core;
public static class RevitMessageManager
{
    public static void SendInfo(long chatId, string message)
    {
        try
        {
            Uri baseAddress = new Uri("net.tcp://localhost:9001/"); 
            EndpointAddress endpoint = new EndpointAddress(baseAddress);
            Binding binding = new NetTcpBinding(SecurityMode.None);
 
            using (ChannelFactory<IRevitService> client = new ChannelFactory<IRevitService>(binding, endpoint))
            {
                IRevitService proxy = client.CreateChannel();

                if (proxy is IClientChannel channel)
                {
                    try
                    {
                        proxy.SendMessageAsync(chatId, message).Wait();
                        Log.Debug($"Sending message: {message}");
                    }
                    catch (Exception ex)
                    {
                        Log.Error(ex, $"Failed to send: {ex.Message}");
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
        }
        catch (Exception ex)
        {
            Log.Error(ex, $"{ex.Message}");
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
