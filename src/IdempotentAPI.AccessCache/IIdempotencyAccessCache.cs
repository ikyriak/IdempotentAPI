using System;
using System.Threading;
using System.Threading.Tasks;
using IdempotentAPI.DistributedAccessLock.Abstractions;

namespace IdempotentAPI.AccessCache
{
    public interface IIdempotencyAccessCache
    {
        /// <summary>
        /// Get the value of type byte[] in the cache for the specified key.
        /// The <paramref name="defaultValue"/> will be saved according to the <paramref name="options"/> provided if the key doesn't exist.
        /// </summary>
        /// <param name="key">The cache key which identifies the entry in the cache.</param>
        /// <param name="defaultValue">In case the value is not in the cache this value will be saved and returned instead.</param>
        /// <param name="options">The implementation needed options that will be used during this operation.</param>
        /// <param name="distributedLockTimeout">The lock timeout is required when a <see cref="IDistributedAccessLockProvider"/> is provided.</param>
        /// <param name="cancellationToken">An optional System.Threading.CancellationToken to cancel the operation.</param>
        /// <returns></returns>
        /// <exception cref="ArgumentNullException"></exception>
        /// <exception cref="Exceptions.DistributedLockNotAcquiredException"></exception>
        Task<byte[]> GetOrSet(
            string key,
            byte[] defaultValue,
            object? options,
            TimeSpan? distributedLockTimeout,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Get the value of type byte[] in the cache for the specified key.
        /// The <paramref name="defaultValue"/> will be returned if the key doesn't exist.
        /// </summary>
        /// <param name="key">The cache key which identifies the entry in the cache.</param>
        /// <param name="defaultValue">The default value to return if the value for the given key is not in the cache.</param>
        /// <param name="options">The implementation needed options that will be used during this operation.</param>
        /// <param name="cancellationToken">An optional System.Threading.CancellationToken to cancel the operation.</param>
        /// <returns></returns>
        Task<byte[]> GetOrDefault(
            string key,
            byte[] defaultValue,
            object? options,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Save the <paramref name="value"/> in the cache for the specified <paramref name="key"/> with the provided <paramref name="options"/>.
        /// If a value exists, it will be overwritten.
        /// </summary>
        /// <param name="key">The cache key which identifies the entry in the cache.</param>
        /// <param name="value">The value to save in the cache.</param>
        /// <param name="options">The implementation needed options that will be used during this operation.</param>
        /// <param name="distributedLockTimeout">The lock timeout is required when a <see cref="IDistributedAccessLockProvider"/> is provided.</param>
        /// <param name="cancellationToken">An optional System.Threading.CancellationToken to cancel the operation.</param>
        /// <exception cref="ArgumentNullException"></exception>
        /// <exception cref="Exceptions.DistributedLockNotAcquiredException"></exception>
        Task Set(
            string key,
            byte[] value,
            object? options,
            TimeSpan? distributedLockTimeout,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Create an instance of the options used for the cache entries to expire in <paramref name="expiryTime"/> TimeStamp.
        /// </summary>
        /// <param name="expiryTime">The number of hours that the value will be saved in the cache.</param>
        /// <returns></returns>
        object CreateCacheEntryOptions(TimeSpan expiryTime);

        /// <summary>
        /// Remove the value from the cache for the specified key.
        /// </summary>
        /// <param name="key">The cache key which identifies the entry in the cache.</param>
        /// <param name="distributedLockTimeout">The lock timeout is required when a <see cref="IDistributedAccessLockProvider"/> is provided.</param>
        /// <param name="cancellationToken">An optional System.Threading.CancellationToken to cancel the operation.</param>
        /// <exception cref="ArgumentNullException"></exception>
        /// <exception cref="Exceptions.DistributedLockNotAcquiredException"></exception>
        Task Remove(string key, TimeSpan? distributedLockTimeout, CancellationToken cancellationToken = default);
    }
}
