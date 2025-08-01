using System;
using System.Data;
using System.Data.Odbc;
using System.Diagnostics;

namespace Database.Services
{
    public static class DatabaseTransactionHelper
    {
        public static (T result, TimeSpan elapsed) ExecuteInTransaction<T>(
            string connectionString,
            Func<OdbcConnection, OdbcTransaction, T> operation)
        {
            return ExecuteWithRetry(() =>
            {
                Stopwatch timer = Stopwatch.StartNew();

                using var connection = new OdbcConnection(connectionString);
                using var transaction = connection.BeginTransaction(IsolationLevel.Serializable);

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

        public static (T result, TimeSpan elapsed) ExecuteWithRetry<T>(
            Func<(T result, TimeSpan elapsed)> operation,
            int maxRetries = 3,
            int baseDelayMs = 100)
        {
            Exception lastException = null;
            Stopwatch totalTimer = Stopwatch.StartNew();

            for (int attempt = 1; attempt <= maxRetries; attempt++)
            {
                try
                {
                    var (result, elapsed) = operation();
                    totalTimer.Stop();
                    return (result, totalTimer.Elapsed);
                }
                catch (OdbcException ex) when (IsRetryableException(ex) && attempt < maxRetries)
                {
                    lastException = ex;
                    int delay = baseDelayMs * attempt;
                    System.Threading.Thread.Sleep(delay);
                }
            }

            totalTimer.Stop();
            throw new InvalidOperationException("Database operation failed after retries", lastException);
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