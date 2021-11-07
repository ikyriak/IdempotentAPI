using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.DependencyInjection;

namespace IdempotentAPI.Cache.DistributedCache.Extensions.DependencyInjection
{
    public static class IdempotencyDistributedCacheExtensions
    {
        /// <summary>
        /// Register the <see cref="IdempotencyDistributedCache"/> implementation that uses the already registered <see cref="IDistributedCache"/> implementation.
        /// </summary>
        /// <param name="serviceCollection"></param>
        /// <returns></returns>
        public static IServiceCollection AddIdempotentAPIUsingDistributedCache(this IServiceCollection serviceCollection)
        {
            // Register the DistributedCache implementation of for the IIdempotencyCache
            serviceCollection.AddSingleton<IIdempotencyCache, IdempotencyDistributedCache>();

            return serviceCollection;
        }
    }
}
