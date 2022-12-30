using System;
using IdempotentAPI.AccessCache;
using IdempotentAPI.Core;
using IdempotentAPI.DistributedAccessLock.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace IdempotentAPI.Filters
{
    /// <summary>
    /// Use Idempotent operations on POST and PATCH HTTP methods
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
    public class IdempotentAttribute : Attribute, IFilterFactory
    {
        public bool IsReusable => false;

        public bool Enabled { get; set; }

        public TimeSpan? ExpiryTime { get; set; }

        /// <summary>
        /// When true, only the responses with 2xx HTTP status codes will be cached.
        /// </summary>
        public bool CacheOnlySuccessResponses { get; set; }

        /// <summary>
        /// The time the distributed lock will wait for the lock to be acquired (in milliseconds).
        /// This is Required when a <see cref="IDistributedAccessLockProvider"/> is provided.
        /// </summary>
        public double DistributedLockTimeoutMilli { get; set; } = -1;

        public IFilterMetadata CreateInstance(IServiceProvider serviceProvider)
        {
            var distributedCache = serviceProvider.GetRequiredService<IIdempotencyAccessCache>();
            var logger = serviceProvider.GetService<ILogger<Idempotency>>() ?? NullLogger<Idempotency>.Instance;
            var settings = serviceProvider.GetService<IIdempotencySettings>();
            var keyGenerator = serviceProvider.GetService<IKeyGenerator>() ?? new DefaultKeyGenerator();
            var requestIdProvider = serviceProvider.GetService<IRequestIdProvider>() ?? new DefaultRequestIdProvider(settings);
            var responseMapper = serviceProvider.GetService<IResponseMapper>() ?? new DefaultResponseMapper();

            var distributedLockTimeout = DistributedLockTimeoutMilli >= 0
                ? TimeSpan.FromMilliseconds(DistributedLockTimeoutMilli)
                : settings.DistributedLockTimeout;

            var instanceSettings = new IdempotencySettings
            {
                CacheOnlySuccessResponses = CacheOnlySuccessResponses || settings.CacheOnlySuccessResponses,
                DistributedCacheKeysPrefix = settings.DistributedCacheKeysPrefix,
                DistributedLockTimeout = distributedLockTimeout,
                HeaderKeyName = settings.HeaderKeyName,
                ExpiryTime = ExpiryTime.GetValueOrDefault(settings.ExpiryTime),
                Enabled = Enabled || settings.Enabled
            };

            return new IdempotencyAttributeFilter(distributedCache, instanceSettings, keyGenerator, requestIdProvider, responseMapper, logger);
        }
    }
}
