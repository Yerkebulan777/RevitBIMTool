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
        private static readonly Lazy<int> _commandTimeout = new(() => GetConfigInt("DatabaseCommandTimeout", 60));
        private static readonly Lazy<int> _maxRetryAttempts = new(() => GetConfigInt("DatabaseMaxRetryAttempts", 5));
        private static readonly Lazy<int> _baseRetryDelayMs = new(() => GetConfigInt("DatabaseRetryDelayMs", 50));

        public static int CommandTimeout => _commandTimeout.Value;
        public static int MaxRetryAttempts => _maxRetryAttempts.Value;
        public static string ConnectionString => _connectionString.Value;

        // Optimized retry delays for PostgreSQL serialization conflicts
        private static readonly TimeSpan[] PostgreSQLRetryDelays = {
                                TimeSpan.Zero,
                                TimeSpan.FromMilliseconds(50),
                                TimeSpan.FromMilliseconds(100),
                                TimeSpan.FromMilliseconds(200),
                                TimeSpan.FromMilliseconds(400)
                            };


        public static (T result, TimeSpan elapsed) RunInTransaction<T>(Func<OdbcConnection, OdbcTransaction, T> operation)
        {
            Exception lastException = null;
            Stopwatch totalTimer = Stopwatch.StartNew();

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
                        totalTimer.Stop();

                        LogPerformanceMetrics(attempt, attemptTimer.Elapsed, true);
                        return (result, totalTimer.Elapsed);
                    }
                    catch
                    {
                        transaction.Rollback();
                        throw;
                    }
                }
                catch (OdbcException ex) when (IsRetryableError(ex) && attempt < MaxRetryAttempts)
                {
                    lastException = ex;
                    TimeSpan delay = GetRetryDelay(attempt);
                    LogRetryAttempt(attempt, ex.Message, delay);
                    Thread.Sleep(delay);
                }
            }

            totalTimer.Stop();
            throw new InvalidOperationException($"Transaction failed after {MaxRetryAttempts} attempts", lastException);
        }

        public static (T result, TimeSpan elapsed) QuerySingle<T>(string sql, object parameters = null)
        {
            return RunInTransaction((connection, transaction) =>
            {
                return connection.QuerySingle<T>(sql, parameters, transaction, CommandTimeout);
            });
        }

        public static (T result, TimeSpan elapsed) QuerySingleOrDefault<T>(string sql, object parameters = null)
        {
            return RunInTransaction((connection, transaction) =>
            {
                return connection.QuerySingleOrDefault<T>(sql, parameters, transaction, CommandTimeout);
            });
        }

        public static (int result, TimeSpan elapsed) Execute(string sql, object parameters = null)
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


        private static TimeSpan GetRetryDelay(int attemptNumber)
        {
            if (attemptNumber <= PostgreSQLRetryDelays.Length)
            {
                return PostgreSQLRetryDelays[attemptNumber - 1];
            }

            Random _random = new();

            // Экспоненциальный откат с джиттером для попыток, превышающих заданные задержки
            TimeSpan baseDelay = TimeSpan.FromMilliseconds(_baseRetryDelayMs.Value * Math.Pow(2, attemptNumber - PostgreSQLRetryDelays.Length));
            TimeSpan jitter = TimeSpan.FromMilliseconds(_random.Next(0, (int)(baseDelay.TotalMilliseconds * 0.1)));

            return baseDelay + jitter;
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

        private static void LogPerformanceMetrics(int attemptNumber, TimeSpan duration, bool success)
        {
            if (duration.TotalMilliseconds > 100) // Log slow operations
            {
                Debug.WriteLine($"Transaction attempt {attemptNumber}: {duration.TotalMilliseconds:F1}ms, Success: {success}");
            }
        }

        private static void LogRetryAttempt(int attempt, string errorMessage, TimeSpan delay)
        {
            Debug.WriteLine($"Retry attempt {attempt}, delay: {delay.TotalMilliseconds}ms, error: {errorMessage}");
        }
    }
}