using System;
using System.Threading;
using System.Threading.Tasks;
using IdempotentAPI.DistributedAccessLock.Abstractions;
using RedLockNet;

namespace IdempotentAPI.DistributedAccessLock.RedLockNet
{
    public class RedLockNetDistributedAccessLockProvider : IDistributedAccessLockProvider
    {
        private readonly IDistributedLockFactory _redLockFactory;

        public RedLockNetDistributedAccessLockProvider(IDistributedLockFactory redLockFactory)
        {
            _redLockFactory = redLockFactory ?? throw new ArgumentNullException(nameof(redLockFactory));
        }

        public IDistributedAccessLock TryAcquire(string resourceKey, TimeSpan acquireTimeout, CancellationToken cancellationToken = default)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                return new DisposableDistributedAccessLock<IRedLock>(false, null);
            }

            IRedLock redLock = _redLockFactory.CreateLock(resourceKey, acquireTimeout);

            // The lock was not acquired because there was no quorum (active endpoints) available.
            if (redLock.Status == RedLockStatus.NoQuorum && redLock.InstanceSummary.Error > 0)
            {
                return new DisposableDistributedAccessLock<IRedLock>(new Exception($"No connection is active/available (NoQuorum) to service the lock operation for key {resourceKey}."));
            }

            return new DisposableDistributedAccessLock<IRedLock>(redLock.IsAcquired, redLock);
        }

        public async Task<IDistributedAccessLock> TryAcquireAsync(string resourceKey, TimeSpan acquireTimeout, CancellationToken cancellationToken = default)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                return new DisposableDistributedAccessLock<IRedLock>(false, null);
            }

            IRedLock redLock = await _redLockFactory.CreateLockAsync(resourceKey, acquireTimeout);

            // The lock was not acquired because there was no quorum (active endpoints) available.
            if (redLock.Status == RedLockStatus.NoQuorum && redLock.InstanceSummary.Error > 0)
            {
                return new DisposableDistributedAccessLock<IRedLock>(new Exception($"No connection is active/available (NoQuorum) to service the lock operation for key {resourceKey}."));
            }

            return new DisposableDistributedAccessLock<IRedLock>(redLock.IsAcquired, redLock);
        }
    }
}
