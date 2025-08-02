using Dapper;
using System;
using System.Configuration;
using System.Data;
using System.Data.Odbc;
using System.Diagnostics;
using System.Threading;

namespace Database.Services
{
    public static class TransactionHelper
    {
        private static readonly Lazy<string> _connectionString = new(InitializeConnectionString);
        private static readonly Lazy<int> _commandTimeout = new(() => GetConfigInt("DatabaseCommandTimeout", 30));
        private static readonly Lazy<int> _baseRetryDelayMs = new(() => GetConfigInt("DatabaseRetryDelayMs", 50));
        private static readonly Lazy<int> _maxRetryAttempts = new(() => GetConfigInt("DatabaseMaxRetryAttempts", 5));

        public static int CommandTimeout => _commandTimeout.Value;
        public static int MaxRetryAttempts => _maxRetryAttempts.Value;
        private static int BaseRetryDelayMs => _baseRetryDelayMs.Value;
        public static string ConnectionString => _connectionString.Value;


        public static T RunInTransaction<T>(Func<OdbcConnection, OdbcTransaction, T> operation)
        {
            Exception lastException = null;

            for (int attempt = 1; attempt <= MaxRetryAttempts; attempt++)
            {
                try
                {
                    Stopwatch attemptTimer = Stopwatch.StartNew();

                    using OdbcConnection connection = new(ConnectionString);

                    connection.Open();

                    using OdbcTransaction transaction = connection.BeginTransaction(IsolationLevel.Serializable);

                    try
                    {
                        T result = operation(connection, transaction);

                        transaction.Commit();

                        attemptTimer.Stop();

                        return result;
                    }
                    catch
                    {
                        transaction.Rollback();
                        throw;
                    }
                }
                catch (OdbcException ex) when (IsRetryableError(ex) && attempt < MaxRetryAttempts)
                {
                    TimeSpan delay = GetRetryDelay(attempt, BaseRetryDelayMs);
                    LogRetryAttempt(attempt, ex.Message, delay);
                    lastException = ex;
                    Thread.Sleep(delay);
                }
            }

            throw new InvalidOperationException($"Transaction failed after {MaxRetryAttempts} attempts", lastException);
        }

        public static T QuerySingle<T>(string sql, object parameters = null)
        {
            return RunInTransaction((connection, transaction) =>
            {
                return connection.QuerySingle<T>(sql, parameters, transaction, CommandTimeout);
            });
        }

        public static T QuerySingleOrDefault<T>(string sql, object parameters = null)
        {
            return RunInTransaction((connection, transaction) =>
            {
                return connection.QuerySingleOrDefault<T>(sql, parameters, transaction, CommandTimeout);
            });
        }

        public static int Execute(string sql, object parameters = null)
        {
            return RunInTransaction((connection, transaction) =>
            {
                return connection.Execute(sql, parameters, transaction, CommandTimeout);
            });
        }


        private static bool IsRetryableError(OdbcException ex)
        {
            string message = ex.Message.ToLowerInvariant();
            return message.Contains("could not serialize access") ||
                    message.Contains("serialization failure") ||
                    message.Contains("deadlock detected") ||
                    message.Contains("connection reset") ||
                    message.Contains("timeout expired");
        }


        private static string InitializeConnectionString()
        {
            string connectionString = ConfigurationManager.ConnectionStrings["PrinterDatabase"]?.ConnectionString;

            if (string.IsNullOrWhiteSpace(connectionString))
            {
                throw new InvalidOperationException("Connection string 'PrinterDatabase' not found");
            }

            // Optimize for PostgreSQL via ODBC
            if (!connectionString.Contains("ConnSettings"))
            {
                connectionString += ";ConnSettings=SET statement_timeout=30000;SET lock_timeout=5000;";
            }

            return connectionString;
        }


        private static int GetConfigInt(string key, int defaultValue)
        {
            return int.TryParse(ConfigurationManager.AppSettings[key], out int value) ? value : defaultValue;
        }


        private static TimeSpan GetRetryDelay(int attemptNumber, int baseDelayMs)
        {
            if (attemptNumber > 0)
            {
                baseDelayMs *= attemptNumber;
            }
            return TimeSpan.FromMilliseconds(baseDelayMs);
        }


        private static void LogRetryAttempt(int attempt, string errorMessage, TimeSpan delay)
        {
            Debug.WriteLine($"Retry attempt {attempt}, delay: {delay.TotalMilliseconds}ms, error: {errorMessage}");
        }


    }
}