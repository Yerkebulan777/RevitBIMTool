using System;
using System.Data;

namespace Database.Providers
{
    /// <summary>
    /// Интерфейс провайдера базы данных
    /// Это ключевая абстракция, которая позволяет нам работать с любой базой данных
    /// через единый интерфейс, скрывая специфику конкретного провайдера
    /// </summary>
    public interface IDatabaseProvider
    {
        /// <summary>
        /// Создание подключения к базе данных
        /// Каждый провайдер реализует это по-своему, но интерфейс остается единым
        /// </summary>
        IDbConnection CreateConnection(string connectionString);

        /// <summary>
        /// Получение SQL-скрипта для создания таблицы принтеров
        /// Разные СУБД имеют разный синтаксис, поэтому каждый провайдер 
        /// возвращает свой вариант DDL-команды
        /// </summary>
        string GetCreateTableScript();

        /// <summary>
        /// Получение SQL для резервирования принтера с блокировкой
        /// В разных СУБД блокировки работают по-разному:
        /// - PostgreSQL: SELECT ... FOR UPDATE
        /// - SQL Server: SELECT ... WITH (UPDLOCK, ROWLOCK)
        /// - SQLite: не поддерживает блокировки, используем транзакции
        /// </summary>
        string GetReservePrinterScript();

        /// <summary>
        /// Поддерживает ли данная СУБД строковые блокировки
        /// Это важно знать для выбора стратегии обработки конкурентного доступа
        /// </summary>
        bool SupportsRowLevelLocking { get; }

        /// <summary>
        /// Название провайдера для логирования и диагностики
        /// </summary>
        string ProviderName { get; }
    }
}