using IdempotentAPI.DistributedAccessLock.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace IdempotentAPI.DistributedAccessLock.MadelsonDistributedLock.Extensions.DependencyInjection
{
    public static class MadelsonDistributedLockExtensions
    {
        public static IServiceCollection AddMadelsonDistributedAccessLock(this IServiceCollection serviceCollection)
        {
            serviceCollection.AddSingleton<IDistributedAccessLockProvider, MadelsonDistributedLockProvider>();

            return serviceCollection;
        }
    }
}
