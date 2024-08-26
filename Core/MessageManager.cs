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
        Binding binding = new NetTcpBinding(SecurityMode.None);

        EndpointAddress endpoint = new(new Uri("net.tcp://localhost:9001/"));

        using ChannelFactory<IRevitService> client = new(binding, endpoint);

        IRevitService proxy = client.CreateChannel();

        if (proxy is IClientChannel channel)
        {
            try
            {
                Log.Debug($"State {channel.State}");
                proxy.SendMessage(chatId, message);
                Log.Debug($"State {channel.State}");
            }
            catch (Exception ex)
            {
                Log.Error(ex, ex.Message);
            }

            //Task asyncTask = Task.Run(async () =>
            //{
            //    try
            //    {
            //        Log.Debug($"State before: {channel.State}");
            //        await proxy.SendMessageAsync(chatId, message);
            //        Log.Debug($"State after: {channel.State}");
            //    }
            //    catch (Exception ex)
            //    {
            //        Log.Error(ex, $"{ex.Message}\nStackTrace: {ex.StackTrace}");
            //    }
            //    finally
            //    {
            //        await Task.Yield();
            //    }
            //});

            //asyncTask.RunSynchronously();

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
