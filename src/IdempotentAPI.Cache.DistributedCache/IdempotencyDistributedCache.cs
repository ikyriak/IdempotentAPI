using System;
using System.Threading;
using System.Threading.Tasks;
using IdempotentAPI.Cache.Abstractions;
using Microsoft.Extensions.Caching.Distributed;

namespace IdempotentAPI.Cache.DistributedCache
{
    public class IdempotencyDistributedCache : IIdempotencyCache
    {
        private readonly IDistributedCache _distributedCache;

        public IdempotencyDistributedCache(IDistributedCache distributedCache)
        {
            _distributedCache = distributedCache;
        }

        /// <returns>An object of type <see cref="DistributedCacheEntryOptions"/>.</returns>
        /// <inheritdoc/>
        public object CreateCacheEntryOptions(TimeSpan expiryTime)
        {
            return new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = expiryTime
            };
        }

        /// <inheritdoc/>
        public async Task<byte[]> GetOrDefault(
            string key,
            byte[] defaultValue,
            object? options = null,
            CancellationToken token = default)
        {
            if (key is null)
            {
                throw new ArgumentNullException(nameof(key));
            }

            if (options is not null && options is not DistributedCacheEntryOptions)
            {
                throw new ArgumentNullException(nameof(options));
            }

            byte[] cachedData = await _distributedCache.GetAsync(key, token)
                .ConfigureAwait(false);
            return cachedData is null ? defaultValue : cachedData;
        }

        /// <inheritdoc/>
        public async Task<byte[]> GetOrSet(
            string key,
            byte[] defaultValue,
            object? options = null,
            CancellationToken token = default)
        {
            if (key is null)
            {
                throw new ArgumentNullException(nameof(key));
            }

            if (options is not null && options is not DistributedCacheEntryOptions)
            {
                throw new ArgumentNullException(nameof(options));
            }

            byte[] cachedData = _distributedCache.Get(key);
            if (cachedData is null)
            {
                await _distributedCache.SetAsync(key, defaultValue, (DistributedCacheEntryOptions?)options, token)
                    .ConfigureAwait(false);
                return defaultValue;
            }
            else
            {
                return cachedData;
            }
        }

        public async Task Remove(string key, CancellationToken token = default)
        {
            if (key is null)
            {
                throw new ArgumentNullException(nameof(key));
            }

            await _distributedCache.RemoveAsync(key, token)
                .ConfigureAwait(false);
        }

        /// <inheritdoc/>
        /// <exception cref="ArgumentNullException"></exception>
        public async Task Set(
            string key,
            byte[] value,
            object? options = null,
            CancellationToken token = default)
        {
            if (key is null)
            {
                throw new ArgumentNullException(nameof(key));
            }

            if (options is not null && options is not DistributedCacheEntryOptions)
            {
                throw new ArgumentNullException(nameof(options));
            }

            await _distributedCache.SetAsync(key, value, (DistributedCacheEntryOptions?)options, token)
                .ConfigureAwait(false);
        }
    }
}
