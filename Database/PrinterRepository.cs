using Database.Models;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Odbc;

namespace Database
{
    public interface IPrinterRepository
    {
        IEnumerable<PrinterInfo> GetActivePrinters();
        PrinterInfo GetPrinterById(int printerId);
        IEnumerable<PrinterInfo> GetStuckPrinters(TimeSpan threshold);

        void UpdatePrinterStatus(int printerId, PrinterInfo state);
        void ResetPrinter(int printerId);
        void LogError(int printerId, string errorMessage);

        bool TryAcquirePrinterLock(int printerId);
        void ReleasePrinterLock(int printerId);

        IDbConnection CreateConnection();
        IDbCommand CreateCommand(string sql, IDbConnection connection);
    }


    public class PrinterRepository : IPrinterRepository
    {
        private readonly string _connectionString;

        public PrinterRepository(string connectionString)
        {
            _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
        }

        public IDbConnection CreateConnection()
        {
            return new OdbcConnection(_connectionString);
        }

        public IDbCommand CreateCommand(string sql, IDbConnection connection)
        {
            IDbCommand command = connection.CreateCommand();
            command.CommandText = sql;
            return command;
        }

        public IEnumerable<PrinterInfo> GetActivePrinters()
        {
            const string sql = @"
            SELECT id, name, state, last_update, job_count 
            FROM printer 
            WHERE state IN (?, ?, ?) 
            ORDER BY last_update ASC";

            using IDbConnection connection = CreateConnection();
            connection.Open();

            using IDbCommand command = CreateCommand(sql, connection);
            AddParameter(command, 1, (int)PrinterState.Ready);
            AddParameter(command, 2, (int)PrinterState.Printing);
            AddParameter(command, 3, (int)PrinterState.Paused);

            List<PrinterInfo> printers = new();
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
            const string sql = @"
            UPDATE printer 
            SET state = ?, last_update = CURRENT_TIMESTAMP 
            WHERE id = ?";

            using IDbConnection connection = CreateConnection();
            connection.Open();

            using IDbTransaction transaction = connection.BeginTransaction();
            try
            {
                using IDbCommand command = CreateCommand(sql, connection);
                command.Transaction = transaction;
                AddParameter(command, 1, (int)state.State);
                AddParameter(command, 2, printerId);

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
            const string sql = @"
            UPDATE printer 
            SET locked = TRUE, locked_by = ?, locked_at = CURRENT_TIMESTAMP 
            WHERE id = ? AND locked = FALSE";

            using IDbConnection connection = CreateConnection();
            connection.Open();

            using IDbCommand command = CreateCommand(sql, connection);
            AddParameter(command, 1, Environment.MachineName);
            AddParameter(command, 2, printerId);

            int affectedRows = command.ExecuteNonQuery();
            return affectedRows > 0;
        }

        public void ReleasePrinterLock(int printerId)
        {
            const string sql = @"
            UPDATE printer 
            SET locked = FALSE, locked_by = NULL, locked_at = NULL 
            WHERE id = ?";

            using IDbConnection connection = CreateConnection();
            connection.Open();

            using IDbCommand command = CreateCommand(sql, connection);
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

        private void SetPrinterStateProperties(PrinterInfo printer, IDataReader reader)
        {
            // Используем индексы вместо имен столбцов
            printer.Id = reader.GetInt32(0);           // id
            printer.PrinterName = reader.GetString(1); // name
            printer.State = (PrinterState)reader.GetInt32(2); // state
            printer.LastUpdate = reader.GetDateTime(3); // last_update
            printer.JobCount = reader.GetInt32(4);     // job_count
        }

        // Остальные методы...
        public PrinterInfo GetPrinterById(int printerId)
        {
            throw new NotImplementedException();
        }

        public IEnumerable<PrinterInfo> GetStuckPrinters(TimeSpan threshold)
        {
            throw new NotImplementedException();
        }

        public void ResetPrinter(int printerId)
        {
            throw new NotImplementedException();
        }

        public void LogError(int printerId, string errorMessage)
        {
            throw new NotImplementedException();
        }
    }


}
