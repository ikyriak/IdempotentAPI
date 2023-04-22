using System;
using System.Threading;
using System.Threading.Tasks;
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
        public object CreateCacheEntryOptions(int expireHours)
        {
            return _idempotencyCache.CreateCacheEntryOptions(expireHours);
        }

        /// <inheritdoc/>
        /// <exception cref="ArgumentNullException"></exception>
        public async Task<byte[]> GetOrDefault(string key, byte[] defaultValue, object? options, CancellationToken cancellationToken = default)
        {
            if (key is null)
            {
                throw new ArgumentNullException(nameof(key));
            }

            using (var inProcesslock = new InProcessAccessLock(key))
            {
                return await _idempotencyCache.GetOrDefault(key, defaultValue, options, cancellationToken)
                    .ConfigureAwait(false);
            }
        }

        /// <inheritdoc/>
        public async Task<byte[]> GetOrSet(
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

                    using (IDistributedAccessLock distributedAccessLock = await _distributedAccessLockProvider.TryAcquireAsync(key, distributedLockTimeout.Value, cancellationToken)
                               .ConfigureAwait(false))
                    {
                        if (distributedAccessLock.IsAcquired)
                        {
                            return await _idempotencyCache.GetOrSet(key, defaultValue, options, cancellationToken)
                                .ConfigureAwait(false);
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

                return await _idempotencyCache.GetOrSet(key, defaultValue, options, cancellationToken)
                    .ConfigureAwait(false);
            }
        }

        /// <inheritdoc/>
        public async Task Remove(
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

                    using (IDistributedAccessLock distributedAccessLock = await _distributedAccessLockProvider.TryAcquireAsync(key, distributedLockTimeout.Value, cancellationToken)
                               .ConfigureAwait(false))
                    {
                        if (distributedAccessLock.IsAcquired)
                        {
                            await _idempotencyCache.Remove(key, cancellationToken)
                                .ConfigureAwait(false);
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

                await _idempotencyCache.Remove(key, cancellationToken)
                    .ConfigureAwait(false);
            }
        }

        /// <inheritdoc/>
        public async Task Set(
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

                    using (IDistributedAccessLock distributedAccessLock = await _distributedAccessLockProvider.TryAcquireAsync(key, distributedLockTimeout.Value, cancellationToken)
                               .ConfigureAwait(false))
                    {
                        if (distributedAccessLock.IsAcquired)
                        {
                            await _idempotencyCache.Set(key, value, options, cancellationToken)
                                .ConfigureAwait(false);
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

                await _idempotencyCache.Set(key, value, options, cancellationToken)
                    .ConfigureAwait(false);
            }
        }
    }
}
