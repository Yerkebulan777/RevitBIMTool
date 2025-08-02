using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

namespace Database
{
    public class PrinterResourceManager<TKey>
    {
        private readonly ConcurrentDictionary<TKey, SemaphoreSlim> _semaphores;

        public async Task<bool> TryReservePrinterAsync(TKey printerId, int maxConcurrency, TimeSpan timeout)
        {
            SemaphoreSlim semaphore = _semaphores.GetOrAdd(printerId, _ => new SemaphoreSlim(maxConcurrency, maxConcurrency));

            return await semaphore.WaitAsync(timeout);
        }



    }
}
