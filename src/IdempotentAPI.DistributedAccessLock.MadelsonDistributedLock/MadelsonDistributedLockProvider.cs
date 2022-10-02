using System;
using System.Threading;
using System.Threading.Tasks;
using IdempotentAPI.DistributedAccessLock.Abstractions;
using Medallion.Threading;

namespace IdempotentAPI.DistributedAccessLock.MadelsonDistributedLock
{
    public class MadelsonDistributedLockProvider : IDistributedAccessLockProvider
    {
        private readonly IDistributedLockProvider _synchronizationProvider;

        public MadelsonDistributedLockProvider(IDistributedLockProvider synchronizationProvider)
        {
            _synchronizationProvider = synchronizationProvider ?? throw new ArgumentNullException(nameof(synchronizationProvider));
        }

        public IDistributedAccessLock TryAcquire(string resourceKey, TimeSpan acquireTimeout, CancellationToken cancellationToken = default)
        {
            try
            {
                IDistributedSynchronizationHandle distributedSynchronizationHandle = _synchronizationProvider.AcquireLock(resourceKey, acquireTimeout, cancellationToken);
                return new DisposableDistributedAccessLock<IDistributedSynchronizationHandle>(true, distributedSynchronizationHandle);
            }
            catch (Exception ex)
            {
                return new DisposableDistributedAccessLock<IDistributedSynchronizationHandle>(ex);
            }
        }

        public async Task<IDistributedAccessLock> TryAcquireAsync(string resourceKey, TimeSpan acquireTimeout, CancellationToken cancellationToken = default)
        {
            try
            {
                IDistributedSynchronizationHandle distributedSynchronizationHandle = await _synchronizationProvider.AcquireLockAsync(resourceKey, acquireTimeout, cancellationToken);
                return new DisposableDistributedAccessLock<IDistributedSynchronizationHandle>(true, distributedSynchronizationHandle);
            }
            catch (Exception ex)
            {
                return new DisposableDistributedAccessLock<IDistributedSynchronizationHandle>(ex);
            }
        }
    }
}
