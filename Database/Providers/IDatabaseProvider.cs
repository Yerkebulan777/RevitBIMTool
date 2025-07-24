using System.Data;

namespace Database.Providers
{
    /// <summary>
    /// Интерфейс провайдера базы данных
    /// Это "договор" между нашей библиотекой и конкретными реализациями
    /// Мы определяем ЧТО нужно сделать, а провайдер решает КАК это сделать
    /// </summary>
    public interface IDatabaseProvider
    {
        /// <summary>
        /// Имя провайдера для диагностики
        /// Например: "SQLite", "InMemory", "SqlServer"
        /// </summary>
        string ProviderName { get; }

        /// <summary>
        /// Поддерживает ли провайдер строковые блокировки
        /// Это влияет на стратегию обработки конкурентных запросов
        /// </summary>
        bool SupportsRowLevelLocking { get; }

        /// <summary>
        /// Создание подключения к базе данных
        /// Провайдер знает, как создать правильное подключение для своей СУБД
        /// </summary>
        IDbConnection CreateConnection(string connectionString);

        /// <summary>
        /// SQL для создания таблицы принтеров
        /// Каждая СУБД имеет свой диалект SQL
        /// </summary>
        string GetCreateTableScript();

        /// <summary>
        /// SQL для резервирования принтера с блокировкой
        /// Разные СУБД по-разному реализуют блокировки
        /// </summary>
        string GetReservePrinterScript();

        /// <summary>
        /// Инициализация провайдера (если требуется)
        /// Некоторые провайдеры могут требовать дополнительной настройки
        /// </summary>
        void Initialize(string connectionString);
    }
}