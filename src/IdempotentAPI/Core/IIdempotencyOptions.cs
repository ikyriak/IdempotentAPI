using System;

namespace IdempotentAPI.Core
{
    public interface IIdempotencyOptions
    {
		/// <summary>
		/// The cached idempotent data retention period in hours
		/// </summary>
		[Obsolete("Use the TimeSpan overload")]
		public int ExpireHours { get; set; }

		/// <summary>
		/// The cached idempotent data retention period in hours
		/// </summary>
		public TimeSpan ExpiresIn { get; set; }

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
    }
}
