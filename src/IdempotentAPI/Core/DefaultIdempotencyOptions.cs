﻿using System;

namespace IdempotentAPI.Core
{
    public static class DefaultIdempotencyOptions
    {
        public static readonly TimeSpan ExpiresIn = TimeSpan.FromHours(24);

        public const string DistributedCacheKeysPrefix = "IdempAPI_";

        public const string HeaderKeyName = "IdempotencyKey";

        public const bool CacheOnlySuccessResponses = true;

        public const double DistributedLockTimeoutMilli = -1;

        public const bool IsIdempotencyOptional = false;
    }
}
