using System;
using System.Threading;
using System.Threading.Tasks;

namespace IdempotentAPI.DistributedAccessLock.Abstractions
{
    public interface IDistributedAccessLockProvider
    {
        IDistributedAccessLock TryAcquire(string resourceKey, TimeSpan acquireTimeout, CancellationToken cancellationToken = default);

        Task<IDistributedAccessLock> TryAcquireAsync(string resourceKey, TimeSpan acquireTimeout, CancellationToken cancellationToken = default);
    }
}
