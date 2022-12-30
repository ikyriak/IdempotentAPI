using System;
using System.Threading;

namespace IdempotentAPI.Cache.Abstractions
{
    public interface IIdempotencyCache
    {
        /// <summary>
        /// Get the value of type byte[] in the cache for the specified key.
        /// The <paramref name="defaultValue"/> will be saved according to the <paramref name="options"/> provided if the key doesn't exist.
        /// </summary>
        /// <param name="key">The cache key which identifies the entry in the cache.</param>
        /// <param name="defaultValue">In case the value is not in the cache this value will be saved and returned instead.</param>
        /// <param name="options">The implementation needed options that will be used during this operation.</param>
        /// <param name="token">An optional System.Threading.CancellationToken to cancel the operation.</param>
        /// <returns></returns>
        byte[] GetOrSet(
            string key,
            byte[] defaultValue,
            object? options = null,
            CancellationToken token = default);

        /// <summary>
        /// Get the value of type byte[] in the cache for the specified key.
        /// The <paramref name="defaultValue"/> will be returned if the key doesn't exist.
        /// </summary>
        /// <param name="key">The cache key which identifies the entry in the cache.</param>
        /// <param name="defaultValue">The default value to return if the value for the given key is not in the cache.</param>
        /// <param name="options">The implementation needed options that will be used during this operation.</param>
        /// <param name="token">An optional System.Threading.CancellationToken to cancel the operation.</param>
        /// <returns></returns>
        byte[] GetOrDefault(
            string key,
            byte[] defaultValue,
            object? options = null,
            CancellationToken token = default);

        /// <summary>
        /// Save the <paramref name="value"/> in the cache for the specified <paramref name="key"/> with the provided <paramref name="options"/>.
        /// If a value exists, it will be overwritten.
        /// </summary>
        /// <param name="key">The cache key which identifies the entry in the cache.</param>
        /// <param name="value">The value to save in the cache.</param>
        /// <param name="options">The implementation needed options that will be used during this operation.</param>
        /// <param name="token">An optional System.Threading.CancellationToken to cancel the operation.</param>
        void Set(
            string key,
            byte[] value,
            object? options = null,
            CancellationToken token = default);

        /// <summary>
        /// Create an instance of the options used for the cache entries to expire in <paramref name="expiryTime"/> TimeSpan.
        /// </summary>
        /// <param name="expiryTime"></param>
        /// <returns></returns>
        object CreateCacheEntryOptions(TimeSpan expiryTime);


        /// <summary>
        /// Remove the value from the cache for the specified key.
        /// </summary>
        /// <param name="key">The cache key which identifies the entry in the cache.</param>
        /// <param name="token">An optional System.Threading.CancellationToken to cancel the operation.</param>
        void Remove(string key, CancellationToken token = default);
    }
}
