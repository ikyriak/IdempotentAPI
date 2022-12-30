using System;

namespace IdempotentAPI.Core;

public class IdempotencySettings : IIdempotencySettings
{
    public bool Enabled { get; set; } = true;
    public string HeaderKeyName { get; set; } = "X-Idempotency-Key";
    public string DistributedCacheKeysPrefix { get; set; } = "Api";
    public bool CacheOnlySuccessResponses { get; set; } = true;
    public TimeSpan ExpiryTime { get; set; } = TimeSpan.FromHours(24);
    public TimeSpan? DistributedLockTimeout { get; set; }
    public string RequestIdHeader { get; set; } = "X-Request-Id";
    public string OriginalRequestIdHeader { get; set; } = "X-Original-Request-Id";
}