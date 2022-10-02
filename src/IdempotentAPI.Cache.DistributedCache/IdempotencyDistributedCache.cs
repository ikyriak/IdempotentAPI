using System;
using System.Threading;
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
        public object CreateCacheEntryOptions(int expireHours)
        {
            return new DistributedCacheEntryOptions()
            {
                AbsoluteExpirationRelativeToNow = new TimeSpan(expireHours, 0, 0)
            };
        }


        /// <inheritdoc/>
        public byte[] GetOrDefault(
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
            return cachedData is null ? defaultValue : cachedData;
        }

        /// <inheritdoc/>
        public byte[] GetOrSet(
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
                _distributedCache.Set(key, defaultValue, (DistributedCacheEntryOptions?)options);
                return defaultValue;
            }
            else
            {
                return cachedData;
            }
        }

        public void Remove(string key, CancellationToken token = default)
        {
            if (key is null)
            {
                throw new ArgumentNullException(nameof(key));
            }

            _distributedCache.Remove(key);
        }

        /// <inheritdoc/>
        /// <exception cref="ArgumentNullException"></exception>
        public void Set(
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

            _distributedCache.Set(key, value, (DistributedCacheEntryOptions?)options);
        }
    }
}
