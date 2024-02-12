using System;
using IdempotentAPI.AccessCache;
using IdempotentAPI.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace IdempotentAPI.Extensions.DependencyInjection
{
    public static class IdempotentAPIExtensions
    {
        /// <summary>
        /// Register the Core services that are required by the IdempotentAPI. Currently, it only registers the service to access the cache (<see cref="IIdempotencyAccessCache"/>).
        /// </summary>
        /// <param name="serviceCollection"></param>
        /// <returns></returns>
        public static IServiceCollection AddIdempotentAPI(this IServiceCollection serviceCollection)
        {
            serviceCollection.AddSingleton<IIdempotencyAccessCache, IdempotencyAccessCache>();

            return serviceCollection;
        }

        /// <summary>
        /// Register the Core services that are required by the IdempotentAPI for Minimal APIs.
        /// </summary>
        /// <param name="serviceCollection"></param>
        /// <returns></returns>
        public static IServiceCollection AddIdempotentMinimalAPI(this IServiceCollection serviceCollection, IdempotencyOptions idempotencyOptions)
        {
            serviceCollection.AddSingleton<IIdempotencyAccessCache, IdempotencyAccessCache>();
            serviceCollection.AddSingleton<IIdempotencyOptions>(idempotencyOptions);
            serviceCollection.AddTransient(serviceProvider =>
            {
                var distributedCache = serviceProvider.GetRequiredService<IIdempotencyAccessCache>();
                var logger = serviceProvider.GetRequiredService<ILogger<Idempotency>>();
                var idempotencyOptions = serviceProvider.GetRequiredService<IIdempotencyOptions>();

                return new Idempotency(
                    distributedCache,
                    logger,
                    idempotencyOptions.ExpiresInMilliseconds,
                    idempotencyOptions.HeaderKeyName,
                    idempotencyOptions.DistributedCacheKeysPrefix,
                    TimeSpan.FromMilliseconds(idempotencyOptions.DistributedLockTimeoutMilli),
                    idempotencyOptions.CacheOnlySuccessResponses,
                    idempotencyOptions.IsIdempotencyOptional);
            });

            return serviceCollection;
        }
    }
}
