using Database.Models;
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
            const string sql = @"
            SELECT id, name, state, last_update, job_count 
            FROM printer 
            WHERE state IN (?, ?, ?) 
            ORDER BY last_update ASC";

            using IDbConnection connection = new OdbcConnection(_connectionString);
            connection.Open();

            using IDbCommand command = CreateCommand(sql, connection);

            AddParameter(command, 0, (int)PrinterState.Ready);
            AddParameter(command, 1, (int)PrinterState.Printing);
            AddParameter(command, 2, (int)PrinterState.Paused);

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
            const string sql = @"
            UPDATE printer 
            SET state = ?, last_update = CURRENT_TIMESTAMP 
            WHERE id = ?";

            using IDbConnection connection = new OdbcConnection(_connectionString);

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

            using IDbConnection connection = new OdbcConnection(_connectionString);
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

            using IDbConnection connection = new OdbcConnection(_connectionString);
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

        private static void SetPrinterStateProperties(PrinterInfo printer, IDataReader reader)
        {
            printer.Id = reader.GetInt32(0);           // id
            printer.PrinterName = reader.GetString(1); // name
            printer.State = (PrinterState)reader.GetInt32(2); // state
            printer.LastUpdate = reader.GetDateTime(3); // last_update
            printer.JobCount = reader.GetInt32(4);     // job_count
        }



    }
}
