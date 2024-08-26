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
            Task asyncTask = Task.Run(async () =>
            {
                try
                {
                    Log.Debug($"State before: {channel.State}");
                    await proxy.SendMessageAsync(chatId, message);
                    Log.Debug("The message was sent successfully");
                }
                catch (Exception ex)
                {
                    Log.Error(ex, $"{ex.Message}\nStackTrace: {ex.StackTrace}");
                }
                finally
                {
                    Log.Debug($"State after: {channel.State}");

                    if (channel.State == CommunicationState.Faulted)
                    {
                        channel.Abort();
                    }
                    else
                    {
                        channel.Close();
                    }
                }
            });

            asyncTask.RunSynchronously();

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
