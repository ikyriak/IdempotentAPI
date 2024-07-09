using System;
using IdempotentAPI.AccessCache;
using IdempotentAPI.Core;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace IdempotentAPI.MinimalAPI.Extensions.DependencyInjection
{
    public static class IdempotentMinimalAPIExtensions
    {
        /// <summary>
        /// Register the Core services that are required by the IdempotentAPI for Minimal APIs.
        /// </summary>
        /// <param name="serviceCollection"></param>
        /// <returns></returns>
        public static IServiceCollection AddIdempotentMinimalAPI(this IServiceCollection serviceCollection, IIdempotencyOptionsProvider idempotencyOptionsProvider)
        {
            serviceCollection.AddHttpContextAccessor();
            serviceCollection.AddSingleton<IIdempotencyAccessCache, IdempotencyAccessCache>();
            serviceCollection.AddTransient(serviceProvider =>
            {
                var distributedCache = serviceProvider.GetRequiredService<IIdempotencyAccessCache>();
                var logger = serviceProvider.GetRequiredService<ILogger<Idempotency>>();

                var httpContextAccessor = serviceProvider.GetRequiredService<IHttpContextAccessor>();
                var idempotencyOptions = idempotencyOptionsProvider.GetIdempotencyOptions(httpContextAccessor);

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
