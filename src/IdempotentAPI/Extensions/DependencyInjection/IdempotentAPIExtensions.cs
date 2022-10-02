using IdempotentAPI.AccessCache;
using Microsoft.Extensions.DependencyInjection;

namespace IdempotentAPI.Extensions.DependencyInjection
{
    public static class IdempotentAPIExtensions
    {
        /// <summary>
        /// Register the Core services that are required by the IdempotentAPI. Currently, it only registers the service to access the cache (<see cref="IIdempotencyAccessCache"/>).
        /// </summary>
        /// <param name="serviceCollection"></param>
        /// <returns></returns>
        public static IServiceCollection AddIdempotentAPI(this IServiceCollection serviceCollection)
        {
            serviceCollection.AddSingleton<IIdempotencyAccessCache, IdempotencyAccessCache>();

            return serviceCollection;
        }
    }
}
