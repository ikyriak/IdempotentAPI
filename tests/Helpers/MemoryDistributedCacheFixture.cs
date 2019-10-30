using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Text;

namespace IdempotentAPI.Tests.Helpers
{
    public class MemoryDistributedCacheFixture : IDisposable
    {
        public MemoryDistributedCache Cache { get; private set; }

        public MemoryDistributedCacheFixture()
        {
            Cache = new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions()));
        }

        public void Dispose()
        {
            
        }
    }
}
