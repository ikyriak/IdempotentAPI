using System;

namespace IdempotentAPI.DistributedAccessLock.Abstractions
{
    public interface IDistributedAccessLock : IDisposable, IAsyncDisposable
    {
        bool IsAcquired { get; }

        Exception? Exception { get; }
    }
}