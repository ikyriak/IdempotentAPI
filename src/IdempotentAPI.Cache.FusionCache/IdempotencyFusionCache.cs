using System;
using System.Threading;
using IdempotentAPI.Cache.Abstractions;
using ZiggyCreatures.Caching.Fusion;

namespace IdempotentAPI.Cache.FusionCache
{
    public class IdempotencyFusionCache : IIdempotencyCache
    {
        private readonly IFusionCache _fusionCache;

        public IdempotencyFusionCache(IFusionCache fusionCache)
        {
            _fusionCache = fusionCache;
        }

        /// <returns>An object of type <see cref="FusionCacheEntryOptions"/>.</returns>
        /// <inheritdoc/>
        public object CreateCacheEntryOptions(TimeSpan expiryTime)
        {
            return new FusionCacheEntryOptions(expiryTime);
        }

        /// <inheritdoc/>
        /// <exception cref="ArgumentNullException"></exception>
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

            if (options is not null && options is not FusionCacheEntryOptions)
            {
                throw new ArgumentNullException(nameof(options));
            }

            return _fusionCache.GetOrDefault(key, defaultValue, (FusionCacheEntryOptions?)options, token);
        }

        /// <inheritdoc/>
        /// <exception cref="ArgumentNullException"></exception>
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

            if (options is not null && options is not FusionCacheEntryOptions)
            {
                throw new ArgumentNullException(nameof(options));
            }

            return _fusionCache.GetOrSet(key, defaultValue, (FusionCacheEntryOptions?)options, token);
        }

        public void Remove(string key, CancellationToken token = default)
        {
            if (key is null)
            {
                throw new ArgumentNullException(nameof(key));
            }

            _fusionCache.Remove(key, token: token);
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

            if (options is not null && options is not FusionCacheEntryOptions)
            {
                throw new ArgumentNullException(nameof(options));
            }

            _fusionCache.Set(key, value, (FusionCacheEntryOptions?)options, token);
        }
    }
}
