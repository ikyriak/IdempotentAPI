using System;
using System.Threading;
using IdempotentAPI.AccessCache.Exceptions;
using IdempotentAPI.AccessCache.Lockers;
using IdempotentAPI.Cache.Abstractions;
using IdempotentAPI.DistributedAccessLock.Abstractions;

namespace IdempotentAPI.AccessCache
{
    public class IdempotencyAccessCache : IIdempotencyAccessCache
    {
        private readonly IIdempotencyCache _idempotencyCache;
        private readonly IDistributedAccessLockProvider? _distributedAccessLockProvider;

        public IdempotencyAccessCache(
            IIdempotencyCache idempotencyCache,
            IDistributedAccessLockProvider? distributedAccessLockProvider = null)
        {
            _idempotencyCache = idempotencyCache;
            _distributedAccessLockProvider = distributedAccessLockProvider;
        }

        /// <inheritdoc/>
        public object CreateCacheEntryOptions(TimeSpan expiryTime)
        {
            return _idempotencyCache.CreateCacheEntryOptions(expiryTime);
        }

        /// <inheritdoc/>
        /// <exception cref="ArgumentNullException"></exception>
        public byte[] GetOrDefault(string key, byte[] defaultValue, object? options, CancellationToken cancellationToken = default)
        {
            if (key is null)
            {
                throw new ArgumentNullException(nameof(key));
            }

            using (var inProcesslock = new InProcessAccessLock(key))
            {
                return _idempotencyCache.GetOrDefault(key, defaultValue, options, cancellationToken);
            }
        }

        /// <inheritdoc/>
        public byte[] GetOrSet(
            string key,
            byte[] defaultValue,
            object? options,
            TimeSpan? distributedLockTimeout,
            CancellationToken cancellationToken = default)
        {
            if (key is null)
            {
                throw new ArgumentNullException(nameof(key));
            }

            using (var inProcesslock = new InProcessAccessLock(key))
            {
                // A distributed lock provider exists:
                if (_distributedAccessLockProvider != null)
                {
                    if (!distributedLockTimeout.HasValue)
                    {
                        throw new ArgumentNullException("To use the IDistributedAccessLockProvider a `distributedLockTimeout` value should be provided.");
                    }

                    using (IDistributedAccessLock distributedAccessLock = _distributedAccessLockProvider.TryAcquire(key, distributedLockTimeout.Value, cancellationToken))
                    {
                        if (distributedAccessLock.IsAcquired)
                        {
                            return _idempotencyCache.GetOrSet(key, defaultValue, options, cancellationToken);
                        }
                        else
                        {
                            if (distributedAccessLock.Exception is null)
                                throw new DistributedLockNotAcquiredException($"Could not acquired distributed Lock for {key} in {distributedLockTimeout}.");
                            else
                                throw new DistributedLockNotAcquiredException($"Could not acquired distributed Lock for {key} in {distributedLockTimeout}.", distributedAccessLock.Exception);
                        }
                    }
                }

                return _idempotencyCache.GetOrSet(key, defaultValue, options, cancellationToken);
            }
        }

        /// <inheritdoc/>
        public void Remove(
            string key,
            TimeSpan? distributedLockTimeout,
            CancellationToken cancellationToken = default)
        {
            if (key is null)
            {
                throw new ArgumentNullException(nameof(key));
            }

            using (var inProcesslock = new InProcessAccessLock(key))
            {
                // A distributed lock provider exists:
                if (_distributedAccessLockProvider != null)
                {
                    if (!distributedLockTimeout.HasValue)
                    {
                        throw new ArgumentNullException("To use the IDistributedAccessLockProvider a `distributedLockTimeout` value should be provided.");
                    }

                    using (IDistributedAccessLock distributedAccessLock = _distributedAccessLockProvider.TryAcquire(key, distributedLockTimeout.Value, cancellationToken))
                    {
                        if (distributedAccessLock.IsAcquired)
                        {
                            _idempotencyCache.Remove(key, cancellationToken);
                            return;
                        }
                        else
                        {
                            if (distributedAccessLock.Exception is null)
                                throw new DistributedLockNotAcquiredException($"Could not acquired distributed Lock for {key} in {distributedLockTimeout}.");
                            else
                                throw new DistributedLockNotAcquiredException($"Could not acquired distributed Lock for {key} in {distributedLockTimeout}.", distributedAccessLock.Exception);
                        }
                    }
                }

                _idempotencyCache.Remove(key, cancellationToken);
            }
        }

        /// <inheritdoc/>
        public void Set(
            string key,
            byte[] value,
            object? options,
            TimeSpan? distributedLockTimeout,
            CancellationToken cancellationToken = default)
        {
            if (key is null)
            {
                throw new ArgumentNullException(nameof(key));
            }

            using (var inProcesslock = new InProcessAccessLock(key))
            {
                // A distributed lock provider exists:
                if (_distributedAccessLockProvider != null)
                {
                    if (!distributedLockTimeout.HasValue)
                    {
                        throw new ArgumentNullException("To use the IDistributedAccessLockProvider a `distributedLockTimeout` value should be provided.");
                    }

                    using (IDistributedAccessLock distributedAccessLock = _distributedAccessLockProvider.TryAcquire(key, distributedLockTimeout.Value, cancellationToken))
                    {
                        if (distributedAccessLock.IsAcquired)
                        {
                            _idempotencyCache.Set(key, value, options, cancellationToken);
                            return;
                        }
                        else
                        {
                            if (distributedAccessLock.Exception is null)
                                throw new DistributedLockNotAcquiredException($"Could not acquired distributed Lock for {key} in {distributedLockTimeout}.");
                            else
                                throw new DistributedLockNotAcquiredException($"Could not acquired distributed Lock for {key} in {distributedLockTimeout}.", distributedAccessLock.Exception);
                        }
                    }
                }

                _idempotencyCache.Set(key, value, options, cancellationToken);
            }
        }
    }
}
