using System;
using System.Threading;
using System.Threading.Tasks;
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

            if (options is not null && options is not FusionCacheEntryOptions)
            {
                throw new ArgumentNullException(nameof(options));
            }

            return await _fusionCache.GetOrDefaultAsync(key, defaultValue, (FusionCacheEntryOptions?)options, token)
                .ConfigureAwait(false);
        }

        /// <inheritdoc/>
        /// <exception cref="ArgumentNullException"></exception>
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

            if (options is not null && options is not FusionCacheEntryOptions)
            {
                throw new ArgumentNullException(nameof(options));
            }

            return await _fusionCache.GetOrSetAsync(key, defaultValue, (FusionCacheEntryOptions?)options, token)
                .ConfigureAwait(false);
        }

        public async Task Remove(string key, CancellationToken token = default)
        {
            if (key is null)
            {
                throw new ArgumentNullException(nameof(key));
            }

            await _fusionCache.RemoveAsync(key, token: token)
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

            if (options is not null && options is not FusionCacheEntryOptions)
            {
                throw new ArgumentNullException(nameof(options));
            }

            await _fusionCache.SetAsync(key, value, (FusionCacheEntryOptions?)options, token)
                .ConfigureAwait(false);
        }
    }
}
