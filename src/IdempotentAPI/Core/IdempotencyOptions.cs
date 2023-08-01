using System;

namespace IdempotentAPI.Core
{
    public class IdempotencyOptions : IIdempotencyOptions
    {
        ///<inheritdoc/>
        public TimeSpan ExpireHours { get; set; } = DefaultIdempotencyOptions.ExpireHours;

        ///<inheritdoc/>
        public string DistributedCacheKeysPrefix { get; set; } = DefaultIdempotencyOptions.DistributedCacheKeysPrefix;

        ///<inheritdoc/>
        public string HeaderKeyName { get; set; } = DefaultIdempotencyOptions.HeaderKeyName;

        ///<inheritdoc/>
        public bool CacheOnlySuccessResponses { get; set; } = DefaultIdempotencyOptions.CacheOnlySuccessResponses;

        ///<inheritdoc/>
        public double DistributedLockTimeoutMilli { get; set; } = DefaultIdempotencyOptions.DistributedLockTimeoutMilli;
    }
}
