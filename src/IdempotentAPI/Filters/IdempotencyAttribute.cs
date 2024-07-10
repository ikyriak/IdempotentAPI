using System;
using IdempotentAPI.AccessCache;
using IdempotentAPI.Core;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace IdempotentAPI.Filters
{
    /// <summary>
    /// Use Idempotent operations on POST and PATCH HTTP methods
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
    public class IdempotentAttribute : Attribute, IFilterFactory, IIdempotencyOptions
    {
        private TimeSpan _expiresIn = DefaultIdempotencyOptions.ExpiresIn;

        public bool IsReusable => false;

        public bool Enabled { get; set; } = true;

        ///<inheritdoc/>
        public int ExpireHours
        {
            get => Convert.ToInt32(_expiresIn.TotalHours);
            set => _expiresIn = TimeSpan.FromHours(value);
        }

        ///<inheritdoc/>
        public double ExpiresInMilliseconds
        {
            get => _expiresIn.TotalMilliseconds;
            set => _expiresIn = TimeSpan.FromMilliseconds(value);
        }

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

        /// <summary>
        /// By default, idempotency settings are taken from the attribute properties.
        /// When this flag is set to true, the settings will be taken from the registered <see cref="IIdempotencyOptions"/> in the ServiceCollection
        /// </summary>
        public bool UseIdempotencyOption { get; set; } = false;

        public JsonSerializerSettings? SerializerSettings { get => null; set => throw new NotImplementedException(); }

        public IFilterMetadata CreateInstance(IServiceProvider serviceProvider)
        {
            var distributedCache = (IIdempotencyAccessCache)serviceProvider.GetService(typeof(IIdempotencyAccessCache));
            var loggerFactory = (ILoggerFactory)serviceProvider.GetService(typeof(ILoggerFactory));

            var generalIdempotencyOptions = serviceProvider.GetRequiredService<IIdempotencyOptions>();
            var idempotencyOptions = UseIdempotencyOption ? generalIdempotencyOptions : this;

            TimeSpan? distributedLockTimeout = idempotencyOptions.DistributedLockTimeoutMilli >= 0
                ? TimeSpan.FromMilliseconds(idempotencyOptions.DistributedLockTimeoutMilli)
                : null;

            return new IdempotencyAttributeFilter(
                distributedCache,
                loggerFactory,
                Enabled,
                idempotencyOptions.ExpiresInMilliseconds,
                idempotencyOptions.HeaderKeyName,
                idempotencyOptions.DistributedCacheKeysPrefix,
                distributedLockTimeout,
                idempotencyOptions.CacheOnlySuccessResponses,
                idempotencyOptions.IsIdempotencyOptional,
                generalIdempotencyOptions.SerializerSettings);
        }
    }
}