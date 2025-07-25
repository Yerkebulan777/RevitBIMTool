using Autodesk.Revit.UI;
using System.Configuration;
using System.Text;

namespace RevitBIMTool.Utils
{
    /// <summary>
    /// Помощник для работы с конфигурацией приложения
    /// </summary>
    public static class ConfigurationHelper
    {
        /// <summary>
        /// Получить строку подключения для принтеров
        /// </summary>
        public static string GetPrinterConnectionString()
        {
            // Сначала проверяем environment variable (для production)
            string envConnectionString = Environment.GetEnvironmentVariable("REVIT_PRINTER_DB_CONNECTION");
            if (!string.IsNullOrEmpty(envConnectionString))
            {
                return envConnectionString;
            }

            // Затем читаем из конфигурации
            string provider = GetAppSetting("DefaultDatabaseProvider", "InMemory");

            return provider.ToLowerInvariant() switch
            {
                "postgresql" => GetConnectionString("PrinterDatabase"),
                "inmemory" => GetConnectionString("PrinterDatabaseInMemory"),
                _ => "InMemory"
            };
        }

        /// <summary>
        /// Получить провайдера БД
        /// </summary>
        public static string GetDatabaseProvider()
        {
            return GetAppSetting("DefaultDatabaseProvider", "InMemory");
        }

        /// <summary>
        /// Получить таймаут блокировки принтера
        /// </summary>
        public static TimeSpan GetPrinterLockTimeout()
        {
            int minutes = int.Parse(GetAppSetting("PrinterLockTimeoutMinutes", "10"));
            return TimeSpan.FromMinutes(minutes);
        }

        /// <summary>
        /// Получить интервал очистки блокировок
        /// </summary>
        public static TimeSpan GetLockCleanupInterval()
        {
            int minutes = int.Parse(GetAppSetting("LockCleanupIntervalMinutes", "2"));
            return TimeSpan.FromMinutes(minutes);
        }

        /// <summary>
        /// Получить Chat ID для уведомлений
        /// </summary>
        public static long GetChatId()
        {
            return Properties.Settings.Default.ChatId;
        }

        /// <summary>
        /// Установить Chat ID
        /// </summary>
        public static void SetChatId(long chatId)
        {
            Properties.Settings.Default.ChatId = chatId;
            Properties.Settings.Default.Save();
        }

        private static string GetConnectionString(string name)
        {
            ConnectionStringSettings connectionString = ConfigurationManager.ConnectionStrings[name];
            return connectionString?.ConnectionString ?? "InMemory";
        }

        private static string GetAppSetting(string key, string defaultValue = "")
        {
            return ConfigurationManager.AppSettings[key] ?? defaultValue;
        }

        // Тест конфигурации
        public static void TestConfiguration()
        {
            try
            {
                StringBuilder sb = new();
                sb.AppendLine($"Provider: {GetDatabaseProvider()}");
                sb.AppendLine($"Timeout: {GetPrinterLockTimeout()}");
                sb.AppendLine($"Connection: {GetPrinterConnectionString()}");

                TaskDialog.Show("Configuration Test", sb.ToString());
            }
            catch (Exception ex)
            {
                _ = TaskDialog.Show("Configuration Error", ex.Message);
            }
        }



    }
}