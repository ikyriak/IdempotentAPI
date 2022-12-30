using System;

namespace IdempotentAPI.Core;

public interface IIdempotencySettings
{
    public bool Enabled { get; set; }
    public string HeaderKeyName { get; set; }

    public string DistributedCacheKeysPrefix { get; set; }

    public bool CacheOnlySuccessResponses { get; set; }

    public TimeSpan ExpiryTime { get; set; }

    public TimeSpan? DistributedLockTimeout { get; set; }
}