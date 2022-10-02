using System;
using System.Threading.Tasks;

namespace IdempotentAPI.DistributedAccessLock.Abstractions
{
    public class DisposableDistributedAccessLock<TDisposableDistributedLock> : IDistributedAccessLock
        where TDisposableDistributedLock : IDisposable, IAsyncDisposable
    {
        public DisposableDistributedAccessLock(bool isAcquired, TDisposableDistributedLock? distributedLock)
        {
            IsAcquired = isAcquired;
            DistributedLock = distributedLock;
            Exception = null;
        }

        public DisposableDistributedAccessLock(Exception exception)
        {
            IsAcquired = false;
            DistributedLock = default;
            Exception = exception;
        }

        /// <summary>
        /// Whether the lock has been acquired.
        /// </summary>
        public bool IsAcquired { get; }

        /// <summary>
        /// The exception occurred.
        /// </summary>
        public Exception? Exception { get; set; }

        /// <summary>
        /// The distributed lock of each implementation.
        /// </summary>
        private TDisposableDistributedLock? DistributedLock { get; }

        public void Dispose()
        {
            DistributedLock?.Dispose();
        }

        public ValueTask DisposeAsync()
        {
            if (DistributedLock is not null)
                return DistributedLock.DisposeAsync();
            else
                return default;
        }
    }
}
