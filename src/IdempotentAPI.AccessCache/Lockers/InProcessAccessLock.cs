using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace IdempotentAPI.AccessCache.Lockers
{
    /// <summary>
    /// Value locker
    /// </summary>
    /// <example>
    /// string myValue = "valueToBeLocked"
    /// using (var inProcesslock = new InProcessAccessLock(myValue)){
    ///     // ...
    /// }
    /// </example>
    internal sealed class InProcessAccessLock : IDisposable
    {
        private static readonly ConcurrentDictionary<string, LockDisposeTracker> lockValueMapper = new ();

        private readonly string _lockedValue;

        public InProcessAccessLock(string valueToLock)
        {
            _lockedValue = valueToLock;
        }

        public static async Task<InProcessAccessLock> CreateAsync(string valueToLock)
        {
            var disposeTracker = lockValueMapper.AddOrUpdate(valueToLock, (_) => new LockDisposeTracker(), (_, disposeTracker) => disposeTracker with
            {
                Count = disposeTracker.Count + 1
            });
            await disposeTracker.Semaphore.Value.WaitAsync();
            return new InProcessAccessLock(valueToLock);
        }

        public void Dispose()
        {
            while (true)
            {
                var minlockValueMapperiLock = lockValueMapper[_lockedValue];
                var updatedLock = minlockValueMapperiLock with { Count = minlockValueMapperiLock.Count - 1 };
                if (lockValueMapper.TryUpdate(_lockedValue, updatedLock, minlockValueMapperiLock))
                {
                    if (updatedLock.Count == 0 && lockValueMapper.TryRemove(_lockedValue, out var removedLock))
                    {
                        removedLock.Semaphore.Value.Release();
                        removedLock.Semaphore.Value.Dispose();
                    }
                    else
                    {
                        minlockValueMapperiLock.Semaphore.Value.Release();
                    }
                    break;
                }
            }
        }

        private sealed record LockDisposeTracker()
        {
            public Lazy<SemaphoreSlim> Semaphore { get; } = new(() =>  new SemaphoreSlim(1, 1),
                LazyThreadSafetyMode.ExecutionAndPublication);

            public int Count { get; set; } = 1;
        }
    }
}
