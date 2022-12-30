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

        public bool Enabled { get; set; } = true;

        public TimeSpan ExpiryTime { get; set; } = TimeSpan.FromHours(24);

        public string DistributedCacheKeysPrefix { get; set; } = "IdempAPI_";

        public string HeaderKeyName { get; set; } = "IdempotencyKey";

        /// <summary>
        /// When true, only the responses with 2xx HTTP status codes will be cached.
        /// </summary>
        public bool CacheOnlySuccessResponses { get; set; } = true;

        /// <summary>
        /// The time the distributed lock will wait for the lock to be acquired (in milliseconds).
        /// This is Required when a <see cref="IDistributedAccessLockProvider"/> is provided.
        /// </summary>
        public double DistributedLockTimeoutMilli { get; set; } = -1;

        public IFilterMetadata CreateInstance(IServiceProvider serviceProvider)
        {
            var distributedCache = serviceProvider.GetRequiredService<IIdempotencyAccessCache>();
            var logger = serviceProvider.GetService<ILogger<Idempotency>>() ?? NullLogger<Idempotency>.Instance;

            TimeSpan? distributedLockTimeout = DistributedLockTimeoutMilli >= 0
                ? TimeSpan.FromMilliseconds(DistributedLockTimeoutMilli)
                : null;

            return new IdempotencyAttributeFilter(
                distributedCache,
                logger,
                Enabled,
                ExpiryTime,
                HeaderKeyName,
                DistributedCacheKeysPrefix,
                distributedLockTimeout,
                CacheOnlySuccessResponses);
        }
    }
}
