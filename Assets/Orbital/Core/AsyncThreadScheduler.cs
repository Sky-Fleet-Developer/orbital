using System;
using System.Threading;
using System.Threading.Tasks;

namespace Orbital.Core
{
    public class AsyncThreadScheduler
    {
        private Semaphore _semaphore;
    
        public AsyncThreadScheduler(int maxThreads)
        {
            _semaphore = new Semaphore(maxThreads, maxThreads);
        }

        public async Task Schedule(Action work)
        {
            TaskCompletionSource<bool> completionSource = new ();
            void Execute()
            {
                work();
                completionSource.SetResult(true);
            }
            _semaphore.WaitOne();
            Thread newThread = new Thread(Execute);
            newThread.Start();
            await completionSource.Task;
            _semaphore.Release();
        }

        public async Task<T> Schedule<T>(Func<T> work)
        {
            TaskCompletionSource<T> completionSource = new ();
            void Execute()
            {
                completionSource.SetResult(work());
            }
            _semaphore.WaitOne();
            Thread newThread = new Thread(Execute);
            newThread.Start();
            await completionSource.Task;
            _semaphore.Release();
            return completionSource.Task.Result;
        }
    }
}
