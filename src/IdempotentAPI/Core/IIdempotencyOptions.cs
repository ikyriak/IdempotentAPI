using System;
using Newtonsoft.Json;

namespace IdempotentAPI.Core
{
    public interface IIdempotencyOptions
    {
        /// <summary>
        /// The cached idempotent data retention period in hours
        /// </summary>
        [Obsolete("Use the double Milliseconds overload")]
        public int ExpireHours { get; set; }

        /// <summary>
        /// The cached idempotent data retention period in hours
        /// </summary>
        public double ExpiresInMilliseconds { get; set; }

        public string DistributedCacheKeysPrefix { get; set; }

        public string HeaderKeyName { get; set; }
        /// <summary>
        /// When true, only the responses with 2xx HTTP status codes will be cached.
        /// </summary>
        public bool CacheOnlySuccessResponses { get; set; }

        /// <summary>
        /// The time the distributed lock will wait for the lock to be acquired (in milliseconds).
        /// This is Required when a <see cref="IDistributedAccessLockProvider"/> is provided.
        /// </summary>
        public double DistributedLockTimeoutMilli { get; set; }

        /// <summary>
        /// By default, idempotency is not optional (false). However, there are cases in which we
        /// would need to make it optional, such as when introducing idempotency in an existing
        /// system, which should be backward compatible.
        /// </summary>
        public bool IsIdempotencyOptional { get; set; }

        public JsonSerializerSettings? SerializerSettings{ get; set; }
    }
}
