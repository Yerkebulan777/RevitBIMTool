// Database/Providers/DatabaseProviderFactory.cs
using System;
using System.Collections.Generic;
using System.Linq;

namespace Database.Providers
{
    /// <summary>
    /// Фабрика провайдеров баз данных - центральная точка принятия решений
    /// 
    /// Эта фабрика решает фундаментальную проблему: "Как выбрать правильную базу данных
    /// не заставляя весь код приложения знать о деталях каждой СУБД?"
    /// 
    /// Паттерн Factory Method позволяет:
    /// 1. Скрыть сложность создания объектов от клиентского кода
    /// 2. Легко добавлять поддержку новых баз данных
    /// 3. Автоматически определять тип БД по строке подключения
    /// 4. Обеспечить единообразный интерфейс для всех провайдеров
    /// 
    /// Это особенно важно для RevitBIMTool, где могут использоваться разные БД
    /// в зависимости от инфраструктуры заказчика
    /// </summary>
    public static class DatabaseProviderFactory
    {
        /// <summary>
        /// Реестр зарегистрированных провайдеров - это наш "каталог автомобилей"
        /// 
        /// Словарь использует StringComparer.OrdinalIgnoreCase, что означает:
        /// - "sqlite", "SQLite", "SQLITE" - все будут работать одинаково
        /// - Это делает API более дружелюбным для пользователей
        /// 
        /// Func<IDatabaseProvider> вместо прямых экземпляров провайдеров означает:
        /// - Провайдеры создаются только когда нужны (ленивая инициализация)
        /// - Каждый вызов CreateProvider создает новый экземпляр
        /// - Нет проблем с потокобезопасностью из-за общих состояний
        /// </summary>
        private static readonly Dictionary<string, Func<IDatabaseProvider>> _providers =
            new(StringComparer.OrdinalIgnoreCase)
            {
                // InMemory - отлично для разработки, тестирования и демонстрации
                // Не требует никаких внешних зависимостей, работает сразу "из коробки"
                { "inmemory", () => new InMemoryProvider() },
                { "memory", () => new InMemoryProvider() },
                
                // SQLite - легковесная файловая БД, идеальна для небольших проектов
                // Отлично подходит для RevitBIMTool в простых сценариях
                { "sqlite", () => new SqliteProvider() },
                
                // SQL Server - корпоративная СУБД для больших организаций
                // Используется когда нужна высокая производительность и надежность
                { "sqlserver", () => new SqlServerProvider() },
                { "mssql", () => new SqlServerProvider() },
                
                // PostgreSQL - мощная open-source СУБД
                // Отличная альтернатива SQL Server с расширенными возможностями
                { "postgresql", CreatePostgreSqlProvider },
                { "postgres", CreatePostgreSqlProvider },
                { "pgsql", CreatePostgreSqlProvider },
                
                // Здесь легко добавить поддержку других БД:
                // { "mysql", () => new MySqlProvider() },
                // { "oracle", () => new OracleProvider() },
                // { "mongodb", () => new MongoDbProvider() },
            };

        /// <summary>
        /// Основной метод создания провайдера по явному имени
        /// 
        /// Это наиболее надежный способ получить нужный провайдер,
        /// потому что нет двусмысленности в том, что именно нужно создать
        /// 
        /// Пример использования:
        /// var provider = DatabaseProviderFactory.CreateProvider("postgresql");
        /// </summary>
        /// <param name="providerName">Имя провайдера (регистронезависимо)</param>
        /// <returns>Экземпляр провайдера базы данных</returns>
        /// <exception cref="ArgumentException">Если имя провайдера пустое</exception>
        /// <exception cref="NotSupportedException">Если провайдер не найден</exception>
        public static IDatabaseProvider CreateProvider(string providerName)
        {
            // Проверяем входные данные - принцип "Fail Fast"
            // Лучше упасть сразу с понятной ошибкой, чем потом в неожиданном месте
            if (string.IsNullOrWhiteSpace(providerName))
            {
                throw new ArgumentException(
                    "Provider name cannot be null, empty or whitespace",
                    nameof(providerName));
            }

            // Пытаемся найти фабричный метод в нашем реестре
            if (_providers.TryGetValue(providerName.Trim(), out Func<IDatabaseProvider> factory))
            {
                try
                {
                    // Вызываем фабричный метод для создания провайдера
                    // Каждый вызов создает новый экземпляр
                    return factory();
                }
                catch (Exception ex)
                {
                    // Оборачиваем исключение создания в более понятное сообщение
                    throw new InvalidOperationException(
                        $"Failed to create database provider '{providerName}'. " +
                        $"Provider factory threw an exception: {ex.Message}", ex);
                }
            }

            // Провайдер не найден - даем пользователю понятную ошибку
            string availableProviders = string.Join(", ", _providers.Keys);
            throw new NotSupportedException(
                $"Database provider '{providerName}' is not supported. " +
                $"Available providers: {availableProviders}");
        }

        /// <summary>
        /// Интеллектуальное определение провайдера по строке подключения
        /// 
        /// Это очень удобно для пользователей - они просто передают строку подключения,
        /// а система сама понимает, с какой БД нужно работать
        /// 
        /// Алгоритм анализирует ключевые слова в строке подключения:
        /// - Для PostgreSQL: "host=", "port=", "user id="
        /// - Для SQLite: "data source=" + ".db" или ".sqlite"
        /// - Для SQL Server: "server=" или "data source=" без расширений файлов
        /// 
        /// Пример строк подключения:
        /// PostgreSQL: "Host=localhost;Port=5432;Database=printers;User Id=user;Password=pass"
        /// SQLite: "Data Source=printers.db"
        /// SQL Server: "Server=localhost;Database=printers;Integrated Security=true"
        /// </summary>
        public static IDatabaseProvider CreateProviderFromConnectionString(string connectionString)
        {
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                throw new ArgumentException(
                    "Connection string cannot be null, empty or whitespace",
                    nameof(connectionString));
            }

            // Приводим к нижнему регистру для удобства сравнения
            string lowerConnectionString = connectionString.ToLowerInvariant();

            // PostgreSQL имеет характерные ключевые слова
            // Host= - стандартный параметр PostgreSQL
            // Port= в сочетании с другими параметрами обычно указывает на PostgreSQL
            if (ContainsPostgreSqlMarkers(lowerConnectionString))
            {
                return CreateProvider("postgresql");
            }

            // SQLite всегда использует файлы с характерными расширениями
            if (ContainsSqliteMarkers(lowerConnectionString))
            {
                return CreateProvider("sqlite");
            }

            // SQL Server обычно использует Server= или Data Source= без файловых расширений
            if (ContainsSqlServerMarkers(lowerConnectionString))
            {
                return CreateProvider("sqlserver");
            }

            // Если не удалось определить тип БД автоматически,
            // используем InMemory как самый безопасный вариант для разработки
            // Это позволяет приложению работать даже без настройки БД
            return CreateProvider("inmemory");
        }

        /// <summary>
        /// Создание специального PostgreSQL провайдера
        /// 
        /// Эта функция решает специфическую проблему .NET Standard 2.0:
        /// - Библиотека Database не может иметь прямую зависимость от Npgsql
        /// - Но основной проект RevitBIMTool может иметь эту зависимость
        /// - Поэтому здесь мы создаем заглушку, которая будет переопределена
        /// 
        /// В основном проекте RevitBIMTool этот метод будет заменен на:
        /// private static IDatabaseProvider CreatePostgreSqlProvider()
        /// {
        ///     return new ConcretePostgreSqlProvider(); // с реальным Npgsql
        /// }
        /// </summary>
        private static IDatabaseProvider CreatePostgreSqlProvider()
        {
            // Для .NET Standard 2.0 библиотеки возвращаем заглушку
            // Это позволяет коду компилироваться, но требует переопределения в основном проекте
            throw new NotImplementedException(
                "PostgreSQL provider requires Npgsql package which is not available in this .NET Standard 2.0 library. " +
                "To use PostgreSQL:\n" +
                "1. Add Npgsql package to your main project\n" +
                "2. Create concrete PostgreSQL provider inheriting from PostgreSqlProvider\n" +
                "3. Override CreatePostgreSqlProvider method in your main project\n" +
                "4. Register your provider using RegisterProvider method");
        }

        /// <summary>
        /// Получение списка всех зарегистрированных провайдеров
        /// 
        /// Полезно для:
        /// - Диагностики системы
        /// - Создания пользовательских интерфейсов выбора БД
        /// - Логирования возможностей системы
        /// - Автоматического тестирования всех провайдеров
        /// </summary>
        public static string[] GetAvailableProviders()
        {
            return _providers.Keys.ToArray();
        }

        /// <summary>
        /// Регистрация нового провайдера во время выполнения
        /// 
        /// Это очень мощная возможность - позволяет расширять систему
        /// без изменения исходного кода библиотеки
        /// 
        /// Пример использования:
        /// DatabaseProviderFactory.RegisterProvider("redis", () => new RedisProvider());
        /// </summary>
        /// <param name="name">Имя провайдера (будет приведено к нижнему регистру)</param>
        /// <param name="factory">Фабричный метод для создания провайдера</param>
        public static void RegisterProvider(string name, Func<IDatabaseProvider> factory)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException(
                    "Provider name cannot be null, empty or whitespace",
                    nameof(name));
            }

            // Перезаписываем существующий провайдер, если он есть
            // Это позволяет переопределять стандартные провайдеры
            _providers[name.Trim().ToLowerInvariant()] = factory ?? throw new ArgumentNullException(nameof(factory),
                    "Provider factory cannot be null");
        }

        /// <summary>
        /// Проверка, зарегистрирован ли провайдер с указанным именем
        /// 
        /// Удобно для условной логики в приложении:
        /// if (DatabaseProviderFactory.IsProviderRegistered("postgresql"))
        /// {
        ///     // используем PostgreSQL специфичные возможности
        /// }
        /// </summary>
        public static bool IsProviderRegistered(string providerName)
        {
            return !string.IsNullOrWhiteSpace(providerName) && _providers.ContainsKey(providerName.Trim().ToLowerInvariant());
        }

        #region Методы анализа строки подключения

        /// <summary>
        /// Определение PostgreSQL по характерным параметрам строки подключения
        /// 
        /// PostgreSQL использует специфичные параметры, которые редко встречаются в других СУБД
        /// </summary>
        private static bool ContainsPostgreSqlMarkers(string lowerConnectionString)
        {
            // Host= - стандартный параметр PostgreSQL (в отличие от Server= в SQL Server)
            bool hasHost = lowerConnectionString.Contains("host=");

            // Port= в сочетании с database= обычно указывает на PostgreSQL
            bool hasPort = lowerConnectionString.Contains("port=");
            bool hasDatabase = lowerConnectionString.Contains("database=");

            // User Id= - характерный параметр PostgreSQL (не Username, не User)
            bool hasUserId = lowerConnectionString.Contains("user id=") ||
                           lowerConnectionString.Contains("userid=");

            // Если есть host= - это почти наверняка PostgreSQL
            if (hasHost)
            {
                return true;
            }

            // Если есть комбинация port= + database= + user id= - это PostgreSQL
            return hasPort && hasDatabase && hasUserId;
        }

        /// <summary>
        /// Определение SQLite по файловым расширениям и параметрам
        /// 
        /// SQLite всегда работает с файлами, это его главная отличительная черта
        /// </summary>
        private static bool ContainsSqliteMarkers(string lowerConnectionString)
        {
            // Data Source= с файловыми расширениями
            bool hasDataSource = lowerConnectionString.Contains("data source");
            bool hasDbExtension = lowerConnectionString.Contains(".db") ||
                                lowerConnectionString.Contains(".sqlite") ||
                                lowerConnectionString.Contains(".sqlite3");

            // Специфичные для SQLite параметры
            bool hasSqliteParams = lowerConnectionString.Contains("version=") ||
                                 lowerConnectionString.Contains("journal mode=") ||
                                 lowerConnectionString.Contains("foreign keys=");

            return (hasDataSource && hasDbExtension) || hasSqliteParams;
        }

        /// <summary>
        /// Определение SQL Server по характерным параметрам
        /// 
        /// SQL Server имеет свои специфичные параметры подключения
        /// </summary>
        private static bool ContainsSqlServerMarkers(string lowerConnectionString)
        {
            // Server= - стандартный параметр SQL Server
            bool hasServer = lowerConnectionString.Contains("server=");

            // Data Source= без файловых расширений (не SQLite)
            bool hasDataSource = lowerConnectionString.Contains("data source=") &&
                               !lowerConnectionString.Contains(".db") &&
                               !lowerConnectionString.Contains(".sqlite");

            // Специфичные для SQL Server параметры
            bool hasSqlServerParams = lowerConnectionString.Contains("integrated security=") ||
                                    lowerConnectionString.Contains("trusted_connection=") ||
                                    lowerConnectionString.Contains("initial catalog=") ||
                                    lowerConnectionString.Contains("application name=");

            return hasServer || hasDataSource || hasSqlServerParams;
        }

        #endregion
    }
}