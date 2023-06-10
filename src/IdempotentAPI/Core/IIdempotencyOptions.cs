namespace IdempotentAPI.Core
{
    public interface IIdempotencyOptions
    {
        public int ExpireHours { get; set; }

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
