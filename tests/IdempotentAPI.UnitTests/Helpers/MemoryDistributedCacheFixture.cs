using System;
using System.Collections.Concurrent;
using IdempotentAPI.AccessCache;
using IdempotentAPI.Cache.Abstractions;
using IdempotentAPI.Cache.DistributedCache;
using IdempotentAPI.Cache.FusionCache;
using IdempotentAPI.DistributedAccessLock.Abstractions;
using IdempotentAPI.UnitTests.Enums;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using ZiggyCreatures.Caching.Fusion;

namespace IdempotentAPI.UnitTests.Helpers
{
    public class MemoryDistributedCacheFixture : IDisposable
    {
        private ConcurrentDictionary<int, IIdempotencyAccessCache> _cachingProviders;

        public MemoryDistributedCacheFixture()
        {
            _cachingProviders = new ConcurrentDictionary<int, IIdempotencyAccessCache>();
        }

        private static int GetCachingProviderKey(CacheImplementation cacheImplementation, DistributedAccessLockImplementation accessLockImplementation)
        {
            return $"{cacheImplementation}|{accessLockImplementation}".GetHashCode();
        }

        public IIdempotencyAccessCache GetIdempotencyCache(CacheImplementation cacheImplementation, DistributedAccessLockImplementation accessLockImplementation)
        {
            int key = GetCachingProviderKey(cacheImplementation, accessLockImplementation);
            var idempotencyAccessCache = CreateCacheInstance(cacheImplementation, accessLockImplementation);

            if (_cachingProviders.TryAdd(key, idempotencyAccessCache))
            {
                return _cachingProviders[key];
            }
            else if (_cachingProviders.TryGetValue(key, out IIdempotencyAccessCache accessCache))
            {
                return accessCache;
            }

            throw new Exception($"The IIdempotencyAccessCache has not been created for {cacheImplementation} and {accessLockImplementation}");
        }

        public static IIdempotencyAccessCache CreateCacheInstance(CacheImplementation cacheImplementation, DistributedAccessLockImplementation accessLockImplementation)
        {
            IIdempotencyCache idempotencyCache;
            switch (cacheImplementation)
            {
                case CacheImplementation.FusionCache:
                    {
                        idempotencyCache = new IdempotencyFusionCache(new FusionCache(new FusionCacheOptions()));
                        break;
                    }
                default:
                    {
                        IDistributedCache distributedCache = new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions()));
                        idempotencyCache = new IdempotencyDistributedCache(distributedCache);
                        break;
                    }
            }

            IDistributedAccessLockProvider distributedAccessLock;
            switch (accessLockImplementation)
            {
                case DistributedAccessLockImplementation.RedLockDotNet:
                    {
                        distributedAccessLock = null;
                        break;
                    }
                case DistributedAccessLockImplementation.MadelsonDistributedLock:
                    {
                        distributedAccessLock = null;
                        break;
                    }
                default:
                    {
                        distributedAccessLock = null;
                        break;
                    }
            }

            return new IdempotencyAccessCache(idempotencyCache, distributedAccessLock);
        }

        public void Dispose()
        {
            _cachingProviders.Clear();
        }
    }
}
