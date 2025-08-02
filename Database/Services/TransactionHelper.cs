using Dapper;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Data.Odbc;
using System.Diagnostics;

namespace Database.Services
{
    public static class TransactionHelper
    {
        private static readonly Lazy<int> _commandTimeout = new(() => GetConfigInt("DatabaseCommandTimeout", 30));
        private static readonly Lazy<int> _baseRetryDelayMs = new(() => GetConfigInt("DatabaseRetryDelayMs", 100));
        private static readonly Lazy<int> _maxRetryAttempts = new(() => GetConfigInt("DatabaseMaxRetryAttempts", 10));
        private static readonly Lazy<string> _connectionString = new(InitializeConnectionString);

        public static int CommandTimeout => _commandTimeout.Value;
        public static int MaxRetryAttempts => _maxRetryAttempts.Value;
        public static int BaseRetryDelayMs => _baseRetryDelayMs.Value;
        public static string ConnectionString => _connectionString.Value;


        public static (T result, TimeSpan elapsed) RunInTransaction<T>(Func<OdbcConnection, OdbcTransaction, T> operation)
        {
            return ExecuteWithRetry(() =>
            {
                Stopwatch timer = Stopwatch.StartNew();

                using OdbcConnection connection = new(ConnectionString);
                using OdbcTransaction transaction = connection.BeginTransaction(IsolationLevel.Serializable);

                try
                {
                    T result = operation(connection, transaction);
                    transaction.Commit();

                    timer.Stop();
                    return (result, timer.Elapsed);
                }
                catch
                {
                    transaction.Rollback();
                    timer.Stop();
                    throw;
                }
            });
        }


        public static (T result, TimeSpan elapsed) ExecuteWithRetry<T>(Func<(T result, TimeSpan elapsed)> operation)
        {
            Exception lastException = null;
            Stopwatch totalTimer = Stopwatch.StartNew();

            for (int attempt = 1; attempt <= MaxRetryAttempts; attempt++)
            {
                try
                {
                    (T result, TimeSpan elapsed) = operation();
                    totalTimer.Stop();
                    return (result, totalTimer.Elapsed);
                }
                catch (OdbcException ex) when (IsRetryableException(ex) && attempt < MaxRetryAttempts)
                {
                    lastException = ex;
                    int delay = BaseRetryDelayMs * attempt;
                    System.Threading.Thread.Sleep(delay);
                }
            }

            totalTimer.Stop();
            throw new InvalidOperationException("Database operation failed after retries", lastException);
        }


        public static (T result, TimeSpan elapsed) QuerySingleOrDefault<T>(string sql, object parameters = null)
        {
            return RunInTransaction((connection, transaction) =>
            {
                return connection.QuerySingleOrDefault<T>(sql, parameters, transaction, CommandTimeout);
            });
        }

        public static (IEnumerable<T> result, TimeSpan elapsed) Query<T>(string sql, object parameters = null)
        {
            return RunInTransaction((connection, transaction) =>
            {
                return connection.Query<T>(sql, parameters, transaction, commandTimeout: CommandTimeout);
            });
        }

        public static (int result, TimeSpan elapsed) Execute(string sql, object parameters = null)
        {
            return RunInTransaction((connection, transaction) =>
            {
                return connection.Execute(sql, parameters, transaction, CommandTimeout);
            });
        }

        private static string InitializeConnectionString()
        {
            string connectionString = ConfigurationManager.ConnectionStrings["PrinterDatabase"]?.ConnectionString;

            return string.IsNullOrWhiteSpace(connectionString)
                ? throw new InvalidOperationException("Connection string 'PrinterDatabase' not found in configuration")
                : connectionString;
        }

        private static int GetConfigInt(string key, int defaultValue)
        {
            return int.TryParse(ConfigurationManager.AppSettings[key], out int value) ? value : defaultValue;
        }

        private static bool IsRetryableException(OdbcException ex)
        {
            string message = ex.Message;
            return message.Contains("serialization", StringComparison.OrdinalIgnoreCase) ||
                   message.Contains("deadlock", StringComparison.OrdinalIgnoreCase) ||
                   message.Contains("lock", StringComparison.OrdinalIgnoreCase);
        }
    }
}