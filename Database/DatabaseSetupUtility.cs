using System;
using System.Configuration;

namespace Database
{
    /// <summary>
    /// Консольная утилита для настройки схемы базы данных.
    /// Предназначена для использования администраторами при первоначальном развертывании
    /// или обновлении системы управления принтерами.
    /// </summary>
    public static class DatabaseSetupUtility
    {
        public static void Main(string[] args)
        {
            Console.WriteLine("=== Утилита настройки базы данных системы управления принтерами ===\n");

            try
            {
                string connectionString = GetConnectionString();

                using var schemaManager = new SchemaManager(connectionString);

                Console.WriteLine("Проверка текущего состояния схемы...");
                bool isValid = schemaManager.ValidateSchema();

                if (isValid)
                {
                    Console.WriteLine("\nСхема базы данных уже существует и корректна.");
                    Console.WriteLine("Создание новой схемы не требуется.");
                    return;
                }

                Console.WriteLine("\nСхема базы данных требует создания или обновления.");
                Console.Write("Продолжить создание схемы? (y/n): ");

                string response = Console.ReadLine();
                if (response?.ToLower() != "y" && response?.ToLower() != "yes")
                {
                    Console.WriteLine("Операция отменена пользователем.");
                    return;
                }

                Console.WriteLine("\nСоздание схемы базы данных...");
                schemaManager.CreatePrinterManagementSchema();

                Console.WriteLine("\nПроверка созданной схемы...");
                bool finalValidation = schemaManager.ValidateSchema();

                if (finalValidation)
                {
                    Console.WriteLine("\n✓ Схема базы данных успешно создана и готова к использованию!");
                    Console.WriteLine("Теперь вы можете запускать приложение RevitBIMTool.");
                }
                else
                {
                    Console.WriteLine("\n✗ Возникли проблемы при создании схемы.");
                    Console.WriteLine("Обратитесь к администратору базы данных для диагностики.");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\n✗ Ошибка при настройке базы данных: {ex.Message}");
                Console.WriteLine("\nПроверьте:");
                Console.WriteLine("1. Строку подключения к базе данных");
                Console.WriteLine("2. Права доступа к PostgreSQL серверу");
                Console.WriteLine("3. Доступность сервера базы данных");

                Environment.ExitCode = 1;
            }

            Console.WriteLine("\nНажмите любую клавишу для выхода...");
            Console.ReadKey();
        }

        private static string GetConnectionString()
        {
            string connectionString = ConfigurationManager.ConnectionStrings["PrinterDatabase"]?.ConnectionString;

            if (string.IsNullOrEmpty(connectionString))
            {
                throw new InvalidOperationException(
                    "Строка подключения 'PrinterDatabase' не найдена в конфигурации приложения.");
            }

            return connectionString;
        }
    }
}