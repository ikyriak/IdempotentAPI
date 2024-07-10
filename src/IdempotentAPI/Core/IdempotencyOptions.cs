using System;
using Newtonsoft.Json;

namespace IdempotentAPI.Core
{
    public class IdempotencyOptions : IIdempotencyOptions
    {
        private TimeSpan _expiresIn = DefaultIdempotencyOptions.ExpiresIn;

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

        public JsonSerializerSettings? SerializerSettings { get; set; }
    }
}
