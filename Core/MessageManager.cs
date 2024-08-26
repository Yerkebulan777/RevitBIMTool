using Autodesk.Revit.UI;
using Serilog;
using ServiceLibrary;
using System.ServiceModel;
using System.ServiceModel.Channels;


namespace RevitBIMTool.Core;
public static class MessageManager
{

    public static void SendInfo(long chatId, string message)
    {
        try
        {
            TimeSpan timeStamp = TimeSpan.FromMinutes(5);

            EndpointAddress endpoint = new(new Uri("net.tcp://localhost:9001/"));

            Binding binding = new NetTcpBinding(SecurityMode.None)
            {
                SendTimeout = timeStamp,
                OpenTimeout = timeStamp,
                CloseTimeout = timeStamp,
                ReceiveTimeout = timeStamp,
            };

            using ChannelFactory<IRevitService> client = new(binding, endpoint);

            IRevitService proxy = client.CreateChannel();

            if (proxy is IClientChannel channel)
            {
                try
                {
                    Log.Debug($"Channel state {channel.State}");
                    proxy.SendMessageAsync(chatId, message).Wait();
                    Log.Debug($"The message was sent successfully");
                }
                catch (AggregateException ae)
                {
                    foreach (Exception ex in ae.InnerExceptions)
                    {
                        Log.Error(ex, $"Failed to send: {ex.Message}");
                    }
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
