using System;
using IdempotentAPI.AccessCache;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Logging;

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

        public int ExpireHours { get; set; } = 24;

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
            var distributedCache = (IIdempotencyAccessCache)serviceProvider.GetService(typeof(IIdempotencyAccessCache));
            var loggerFactory = (ILoggerFactory)serviceProvider.GetService(typeof(ILoggerFactory));

            TimeSpan? distributedLockTimeout = DistributedLockTimeoutMilli >= 0
                ? TimeSpan.FromMilliseconds(DistributedLockTimeoutMilli)
                : null;

            return new IdempotencyAttributeFilter(
                distributedCache,
                loggerFactory,
                Enabled,
                ExpireHours,
                HeaderKeyName,
                DistributedCacheKeysPrefix,
                distributedLockTimeout,
                CacheOnlySuccessResponses);
        }
    }
}
