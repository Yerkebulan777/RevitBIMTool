using Dapper;
using Database.Stores;
using System;
using System.Data.Odbc;

namespace Database.Schema
{
    public sealed class SchemaManager : IDisposable
    {
        private readonly string _connectionString;
        private readonly int _commandTimeout;
        private bool _disposed = false;

        public SchemaManager(string connectionString, int commandTimeout = 60)
        {
            _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
            _commandTimeout = commandTimeout;
        }

        public void CreatePrinterManagementSchema()
        {
            using OdbcConnection connection = new OdbcConnection(_connectionString);

            connection.Open();

            using OdbcTransaction transaction = connection.BeginTransaction();

            try
            {
                _ = connection.Execute(PrinterSqlStore.CreatePrinterStatesTable, transaction: transaction, commandTimeout: _commandTimeout);
                AddTableConstraints(connection, transaction);
                transaction.Commit();
                Console.WriteLine("✓ Схема базы данных для управления принтерами успешно создана");
            }
            catch
            {
                transaction.Rollback();
                throw;
            }
        }


        private void AddTableConstraints(OdbcConnection connection, OdbcTransaction transaction)
        {
            foreach (string constraint in PrinterSqlStore.TableConstraints)
            {
                try
                {
                    _ = connection.Execute(constraint, transaction: transaction, commandTimeout: _commandTimeout);
                }
                catch (Exception ex)
                {
                    // Некоторые ограничения могут уже существовать
                    Console.WriteLine($"Warning: {ex.Message}");
                }
            }
        }


        public bool ValidateSchema()
        {
            try
            {
                using OdbcConnection connection = new OdbcConnection(_connectionString);
                connection.Open();

                int tableCount = connection.QuerySingle<int>(PrinterSqlStore.CheckTableExists, commandTimeout: _commandTimeout);

                if (tableCount == 0)
                {
                    Console.WriteLine("✗ Таблица printer_states не найдена");
                    return false;
                }

                int columnCount = connection.QuerySingle<int>(PrinterSqlStore.ValidateSchemaColumns, commandTimeout: _commandTimeout);

                if (columnCount < 4)
                {
                    Console.WriteLine("✗ В таблице printer_states отсутствуют необходимые столбцы");
                    return false;
                }

                Console.WriteLine("✓ Схема базы данных валидна и готова к использованию");
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"✗ Ошибка при валидации схемы: {ex.Message}");
                return false;
            }
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _disposed = true;
            }
        }
    }
}