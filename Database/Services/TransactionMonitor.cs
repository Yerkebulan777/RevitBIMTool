using Database.Logging;
using System;
using System.Collections.Concurrent;
using System.Diagnostics;

namespace Database.Services
{
    public sealed class TransactionMonitor
    {
        private readonly ILogger _logger;
        private readonly ConcurrentDictionary<Guid, TransactionMetrics> _activeTransactions;
        private static readonly Lazy<TransactionMonitor> _instance = new(() => new TransactionMonitor());

        public static TransactionMonitor Instance => _instance.Value;

        private TransactionMonitor()
        {
            _logger = LoggerFactory.CreateLogger<TransactionMonitor>();
            _activeTransactions = new ConcurrentDictionary<Guid, TransactionMetrics>();
        }

        public IDisposable BeginMonitoring(string operationName, string details = null)
        {
            var metrics = new TransactionMetrics
            {
                Id = Guid.NewGuid(),
                OperationName = operationName,
                Details = details,
                StartTime = DateTime.UtcNow,
                Stopwatch = Stopwatch.StartNew()
            };

            _activeTransactions[metrics.Id] = metrics;

            _logger.Information($"Started: {operationName} [{metrics.Id:N}]");

            return new MonitoringScope(this, metrics);
        }

        private void EndMonitoring(TransactionMetrics metrics, bool success)
        {
            metrics.Stopwatch.Stop();
            metrics.EndTime = DateTime.UtcNow;
            metrics.Success = success;

            _activeTransactions.TryRemove(metrics.Id, out _);

            var duration = metrics.Stopwatch.Elapsed;
            var level = duration.TotalMinutes > 5 ? LogLevel.Warning : LogLevel.Information;

            _logger.Log(level,
                $"Completed: {metrics.OperationName} [{metrics.Id:N}] " +
                $"Duration: {duration:mm\\:ss\\.fff}, Success: {success}",
                nameof(EndMonitoring));

            // Отправка метрик если превышен порог
            if (duration.TotalMinutes > 10)
            {
                AlertLongRunningOperation(metrics);
            }
        }

        private void AlertLongRunningOperation(TransactionMetrics metrics)
        {
            _logger.Warning(
                $"LONG OPERATION ALERT: {metrics.OperationName} " +
                $"took {metrics.Stopwatch.Elapsed.TotalMinutes:F1} minutes. " +
                $"Details: {metrics.Details}");
        }

        public void ReportActiveTransactions()
        {
            foreach (var kvp in _activeTransactions)
            {
                var metrics = kvp.Value;
                var duration = metrics.Stopwatch.Elapsed;

                _logger.Information(
                    $"Active: {metrics.OperationName} " +
                    $"Running for: {duration:mm\\:ss}");
            }
        }

        private sealed class MonitoringScope : IDisposable
        {
            private readonly TransactionMonitor _monitor;
            private readonly TransactionMetrics _metrics;
            private bool _disposed;

            public MonitoringScope(TransactionMonitor monitor, TransactionMetrics metrics)
            {
                _monitor = monitor;
                _metrics = metrics;
            }

            public void Dispose()
            {
                if (!_disposed)
                {
                    _monitor.EndMonitoring(_metrics, !_metrics.Failed);
                    _disposed = true;
                }
            }
        }

        private sealed class TransactionMetrics
        {
            public Guid Id { get; set; }
            public string OperationName { get; set; }
            public string Details { get; set; }
            public DateTime StartTime { get; set; }
            public DateTime? EndTime { get; set; }
            public Stopwatch Stopwatch { get; set; }
            public bool Success { get; set; }
            public bool Failed { get; set; }
        }
    }
}