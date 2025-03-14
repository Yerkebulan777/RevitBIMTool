using System;
using System.Threading;


namespace ServiceLibrary.Helpers
{

    public static class ConcurrentActionHandler
    {
        public static void ExecuteWithMutex(string mutexId, Action action)
        {
            using (Mutex mutex = new Mutex(false, mutexId))
            {
                if (mutex.WaitOne(Timeout.Infinite))
                {
                    try
                    {
                        action();
                    }
                    finally
                    {
                        mutex.ReleaseMutex();
                    }
                }
            }
        }
    }


}

