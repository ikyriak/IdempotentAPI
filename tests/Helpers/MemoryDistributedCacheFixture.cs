using System;
using IdempotentAPI.Cache;
using IdempotentAPI.Cache.DistributedCache;
using IdempotentAPI.Cache.FusionCache;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using ZiggyCreatures.Caching.Fusion;

namespace IdempotentAPI.Tests.Helpers
{
    public class MemoryDistributedCacheFixture : IDisposable
    {
        private readonly IIdempotencyCache _fusionCache;
        private readonly IIdempotencyCache _distributedCache;

        public MemoryDistributedCacheFixture()
        {
            _fusionCache = CreateCacheInstance(CacheImplementationEnum.FusionCache);
            _distributedCache = CreateCacheInstance(CacheImplementationEnum.DistributedCache);
        }

        public IIdempotencyCache GetIdempotencyCache(CacheImplementationEnum cacheImplementation)
        {
            switch (cacheImplementation)
            {
                case CacheImplementationEnum.FusionCache:
                    return _fusionCache;
                default:
                    return _distributedCache;
            }
        }

        public static IIdempotencyCache CreateCacheInstance(CacheImplementationEnum cacheImplementation)
        {
            switch (cacheImplementation)
            {
                case CacheImplementationEnum.FusionCache:
                    return new IdempotencyFusionCache(new FusionCache(new FusionCacheOptions()));
                default:
                    {
                        IDistributedCache distributedCache = new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions()));
                        return new IdempotencyDistributedCache(distributedCache);
                    }
            }
        }

        public void Dispose()
        {

        }
    }
}
