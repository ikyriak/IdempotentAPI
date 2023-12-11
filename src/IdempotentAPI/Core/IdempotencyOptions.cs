using System;

namespace IdempotentAPI.Core
{
    public class IdempotencyOptions : IIdempotencyOptions
    {
        private TimeSpan _expiresIn = DefaultIdempotencyOptions.ExpiresIn;

		///<inheritdoc/>
		public int ExpireHours
        {
            get => Convert.ToInt32(this._expiresIn.TotalHours);
            set => this._expiresIn = TimeSpan.FromHours(value);
        }

		///<inheritdoc/>
		public TimeSpan ExpiresIn
        {
            get => this._expiresIn;
            set => this._expiresIn = value;
        }

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
