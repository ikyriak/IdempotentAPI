using System;
using Microsoft.Extensions.DependencyInjection;
using ZiggyCreatures.Caching.Fusion;

namespace IdempotentAPI.Cache.FusionCache.Extensions.DependencyInjection
{
    public static class IdempotencyFusionCacheExtensions
    {
        /// <summary>
        /// Register and configure the FusionCache services that the IdempotentAPI library needs.
        /// <list type="bullet">
        ///     <item>TIP: If the FusionCache services are already registered, then you should use the <see cref="AddIdempotentAPIUsingRegisteredFusionCache"/>.</item>
        /// </list> 
        /// </summary>
        /// <param name="serviceCollection"></param>
        /// <param name="cacheEntryOptions">Read the following URL for instructions: https://github.com/jodydonetti/ZiggyCreatures.FusionCache/blob/main/docs/StepByStep.md</param>
        /// <param name="distributedCacheCircuitBreakerDuration">To temporarily disable the distributed cache in case of hard errors so that, if the distributed cache is having issues, it will have less requests to handle and maybe it will be able to get back on its feet.</param>
        /// <returns></returns>
        public static IServiceCollection AddIdempotentAPIUsingFusionCache(
            this IServiceCollection serviceCollection,
            FusionCacheEntryOptions? cacheEntryOptions = null,
            TimeSpan? distributedCacheCircuitBreakerDuration = null)
        {
            // Register the FusionCache implementation of for the IIdempotencyCache
            serviceCollection.AddSingleton<IIdempotencyCache, IdempotencyFusionCache>();

            // Register the fusion cache serializer
            serviceCollection.AddFusionCacheNewtonsoftJsonSerializer();

            // Register the FusionCache
            serviceCollection.AddFusionCache(options =>
            {
                // Set custom cache options
                if (cacheEntryOptions != null)
                    options.DefaultEntryOptions = cacheEntryOptions;

                // Distibuted cache circuit-breaker
                if (distributedCacheCircuitBreakerDuration.HasValue)
                    options.DistributedCacheCircuitBreakerDuration = distributedCacheCircuitBreakerDuration.Value;
            });

            return serviceCollection;
        }

        /// <summary>
        /// Register the <see cref="IdempotencyFusionCache"/> implementation that uses the already registered <see cref="IFusionCache"/>.
        /// </summary>
        /// <param name="serviceCollection"></param>
        /// <returns></returns>
        public static IServiceCollection AddIdempotentAPIUsingRegisteredFusionCache(this IServiceCollection serviceCollection)
        {
            // Register the FusionCache implementation of for the IIdempotencyCache
            serviceCollection.AddSingleton<IIdempotencyCache, IdempotencyFusionCache>();

            return serviceCollection;
        }
    }
}
