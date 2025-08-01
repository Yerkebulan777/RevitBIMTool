using Database.Logging;
using System;
using System.Data;
using System.Data.Odbc;
using System.Diagnostics;
using System.Text;

namespace Database.Services
{
    public sealed class TransactionHelper
    {
        private readonly ILogger _logger ;
        private readonly string _connectionString;

        public TransactionHelper(string connection, ILogger loger)
        {
            _logger = loger ?? throw new ArgumentNullException(nameof(loger));
            _connectionString = connection ?? throw new ArgumentNullException(nameof(connection));
        }


        public T ExecuteInTransaction<T>(Func<OdbcConnection, OdbcTransaction, T> operation)
        {
            return ExecuteWithRetry(() =>
            {
                using OdbcConnection connection = new(_connectionString);
                using OdbcTransaction transaction = connection.BeginTransaction(IsolationLevel.Serializable);

                try
                {
                    T result = operation(connection, transaction);
                    transaction.Commit();
                    return result;
                }
                catch (Exception ex) 
                {
                    transaction.Rollback();
                    _logger.Error($"Transaction failed: {ex.Message}", ex);
                    throw;
                }
            });
        }


        private T ExecuteWithRetry<T>(Func<T> operation, int maxRetries = 3, int baseDelayMs = 100)
        {
            Exception lastException = null;
            StringBuilder logBuilder = new();
            Stopwatch totalTimer = Stopwatch.StartNew();

            _ = logBuilder.AppendLine("Starting operation with retry logic");

            for (int attempt = 1; attempt <= maxRetries; attempt++)
            {
                try
                {
                    _ = logBuilder.AppendLine($"Operation succeeded on attempt {attempt}");
                    _ = logBuilder.AppendLine($"Total duration: {totalTimer.ElapsedMilliseconds}ms");

                    _logger.Debug(logBuilder.ToString());

                    return operation();
                }
                catch (OdbcException ex) when (IsRetryableException(ex) && attempt < maxRetries)
                {
                    lastException = ex;
                    int delay = baseDelayMs * attempt;
                    System.Threading.Thread.Sleep(delay);
                }
            }

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