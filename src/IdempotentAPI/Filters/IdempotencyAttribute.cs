using System;
using IdempotentAPI.AccessCache;
using IdempotentAPI.Core;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Logging;

namespace IdempotentAPI.Filters
{
    /// <summary>
    /// Use Idempotent operations on POST and PATCH HTTP methods
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
    public class IdempotentAttribute : Attribute, IFilterFactory, IIdempotencyOptions
    {
        public bool IsReusable => false;

        public bool Enabled { get; set; } = true;

        ///<inheritdoc/>
        public int ExpireHours { get; set; } = DefaultIdempotencyOptions.ExpireHours;

        ///<inheritdoc/>
        public string DistributedCacheKeysPrefix { get; set; } = DefaultIdempotencyOptions.DistributedCacheKeysPrefix;

        ///<inheritdoc/>
        public string HeaderKeyName { get; set; } = DefaultIdempotencyOptions.HeaderKeyName;

        ///<inheritdoc/>
        public bool CacheOnlySuccessResponses { get; set; } = DefaultIdempotencyOptions.CacheOnlySuccessResponses;

        ///<inheritdoc/>
        public double DistributedLockTimeoutMilli { get; set; } = DefaultIdempotencyOptions.DistributedLockTimeoutMilli;

        ///<inheritdoc/>
        public bool IsIdempotencyOptional { get; set; } = DefaultIdempotencyOptions.IsIdempotencyOptional;

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
                CacheOnlySuccessResponses,
                IsIdempotencyOptional);
        }
    }
}
