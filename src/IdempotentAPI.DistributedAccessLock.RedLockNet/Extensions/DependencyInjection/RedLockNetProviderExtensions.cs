using System.Collections.Generic;
using System.Net;
using IdempotentAPI.DistributedAccessLock.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using RedLockNet;
using RedLockNet.SERedis;
using RedLockNet.SERedis.Configuration;

namespace IdempotentAPI.DistributedAccessLock.RedLockNet.Extensions.DependencyInjection
{
    public static class RedLockNetProviderExtensions
    {
        public static IServiceCollection AddRedLockNetDistributedAccessLock(
            this IServiceCollection serviceCollection,
            List<DnsEndPoint> redisEndpoints)
        {
            var endPoints = new List<RedLockEndPoint>();
            foreach (var redisEndpoint in redisEndpoints)
            {
                endPoints.Add(redisEndpoint);
            }

            serviceCollection.AddSingleton<IDistributedLockFactory, RedLockFactory>((s) =>
            {
                return RedLockFactory.Create(endPoints);
            });


            serviceCollection.AddSingleton<IDistributedAccessLockProvider, RedLockNetDistributedAccessLockProvider>();


            return serviceCollection;
        }

        public static IServiceCollection AddRedLockNetDistributedAccessLock(
            this IServiceCollection serviceCollection,
            List<RedLockMultiplexer> redLockMultiplexers)
        {
            serviceCollection.AddSingleton<IDistributedLockFactory, RedLockFactory>((s) =>
            {
                return RedLockFactory.Create(redLockMultiplexers);
            });

            serviceCollection.AddSingleton<IDistributedAccessLockProvider, RedLockNetDistributedAccessLockProvider>();


            return serviceCollection;
        }
    }
}
