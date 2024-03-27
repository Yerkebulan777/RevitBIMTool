using RevitBIMTool.Properties;


namespace RevitBIMTool.Core;
public static class SettingsWrapper
{
    public static long BotChatId
    {
        get
        {
            return Settings.Default.ChatId;
        }
        set
        {
            if (!Settings.Default.ChatId.Equals(value))
            {
                Settings.Default.ChatId = value;
                Settings.Default.Save();
            }
        }
    }


    public static void ResetSettings()
    {
        Settings.Default.Reset();
    }

}
