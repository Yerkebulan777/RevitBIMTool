using Database.Models;
using Database.Stores;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Odbc;

namespace Database
{
    public class PrinterRepository(string connectionString)
    {
        private readonly string _connectionString = connectionString;

        public static IDbCommand CreateCommand(string sql, IDbConnection connection)
        {
            IDbCommand command = connection.CreateCommand();
            command.CommandText = sql;
            return command;
        }

        public IEnumerable<PrinterInfo> GetActivePrinters()
        {
            using IDbConnection connection = new OdbcConnection(_connectionString);
            connection.Open();

            using IDbCommand command = CreateCommand(PrinterSqlStore.GetActivePrinters, connection);

            List<PrinterInfo> printers = [];
            using (IDataReader reader = command.ExecuteReader())
            {
                while (reader.Read())
                {
                    PrinterInfo printer = new();
                    SetPrinterStateProperties(printer, reader);
                    printers.Add(printer);
                }
            }
            return printers;
        }

        public void UpdatePrinterStatus(int printerId, PrinterInfo state)
        {
            using IDbConnection connection = new OdbcConnection(_connectionString);
            connection.Open();

            using IDbTransaction transaction = connection.BeginTransaction();
            try
            {
                using IDbCommand command = CreateCommand(PrinterSqlStore.UpdatePrinterStatus, connection);
                command.Transaction = transaction;
                AddParameter(command, 1, printerId);
                _ = command.ExecuteNonQuery();
                transaction.Commit();
            }
            catch
            {
                transaction.Rollback();
                throw;
            }
        }

        public bool TryAcquirePrinterLock(int printerId)
        {
            using IDbConnection connection = new OdbcConnection(_connectionString);
            connection.Open();

            using IDbCommand command = CreateCommand(PrinterSqlStore.TryAcquirePrinterLock, connection);
            AddParameter(command, 1, Environment.MachineName);
            AddParameter(command, 2, printerId);

            int affectedRows = command.ExecuteNonQuery();
            return affectedRows > 0;
        }

        public void ReleasePrinterLock(int printerId)
        {
            using IDbConnection connection = new OdbcConnection(_connectionString);
            connection.Open();

            using IDbCommand command = CreateCommand(PrinterSqlStore.ReleasePrinterLock, connection);
            AddParameter(command, 1, printerId);
            _ = command.ExecuteNonQuery();
        }

        private static void AddParameter(IDbCommand command, int index, object value)
        {
            IDbDataParameter parameter = command.CreateParameter();
            parameter.ParameterName = $"@p{index}";
            parameter.Value = value ?? DBNull.Value;
            _ = command.Parameters.Add(parameter);
        }

        private static void SetPrinterStateProperties(PrinterInfo printer, IDataReader reader)
        {
            printer.Id = reader.GetInt32(0);
            printer.PrinterName = reader.GetString(1);
            printer.State = (PrinterState)reader.GetInt32(2);
            printer.LastUpdate = reader.GetDateTime(3);
            printer.JobCount = reader.GetInt32(4);
        }
    }
}