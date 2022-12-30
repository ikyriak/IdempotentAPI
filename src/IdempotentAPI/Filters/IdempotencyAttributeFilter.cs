using System;
using IdempotentAPI.AccessCache;
using IdempotentAPI.Core;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace IdempotentAPI.Filters
{
    public class IdempotencyAttributeFilter : IActionFilter, IResultFilter
    {
        private readonly bool _enabled;
        private readonly TimeSpan _expiryTime;
        private readonly string _headerKeyName;
        private readonly string _distributedCacheKeysPrefix;
        private readonly TimeSpan? _distributedLockTimeout;
        private readonly bool _cacheOnlySuccessResponses;
        private readonly IIdempotencyAccessCache _distributedCache;
        private readonly ILogger<Idempotency> _logger;

        private Idempotency? _idempotency = null;

        public IdempotencyAttributeFilter(
            IIdempotencyAccessCache distributedCache,
            ILogger<Idempotency> logger,
            bool enabled,
            TimeSpan expiryTime,
            string headerKeyName,
            string distributedCacheKeysPrefix,
            TimeSpan? distributedLockTimeout,
            bool cacheOnlySuccessResponses)
        {
            _distributedCache = distributedCache;
            _enabled = enabled;
            _expiryTime = expiryTime;
            _headerKeyName = headerKeyName;
            _distributedCacheKeysPrefix = distributedCacheKeysPrefix;
            _distributedLockTimeout = distributedLockTimeout;
            _cacheOnlySuccessResponses = cacheOnlySuccessResponses;
            _logger = logger;
        }

        /// <summary>
        /// Runs before the execution of the controller
        /// </summary>
        /// <param name="context"></param>
        public void OnActionExecuting(ActionExecutingContext context)
        {
            // If the Idempotency is disabled then stop
            if (!_enabled)
            {
                return;
            }

            // Initialize only on its null (in case of multiple executions):
            if (_idempotency == null)
            {
                _idempotency = new Idempotency(
                    _distributedCache,
                    _logger,
                    _expiryTime,
                    _headerKeyName,
                    _distributedCacheKeysPrefix,
                    _distributedLockTimeout,
                    _cacheOnlySuccessResponses);
            }

            _idempotency.ApplyPreIdempotency(context);
        }

        /// <summary>
        /// Runs after the execution of the controller. In our case we used it to perform specific actions on Exceptions.
        /// </summary>
        /// <param name="context"></param>
        public void OnActionExecuted(ActionExecutedContext context)
        {
            // If the Idempotency is disabled then stop.
            // Stop if the PreIdempotency step is not applied.
            if (!_enabled || _idempotency == null)
            {
                return;
            }

            if (context?.Exception is not null)
            {
                _idempotency.CancelIdempotency();
            }
        }

        // NOT USED
        public void OnResultExecuting(ResultExecutingContext context)
        {

        }

        /// <summary>
        /// Runs after the results have been calculated
        /// </summary>
        /// <param name="context"></param>
        public void OnResultExecuted(ResultExecutedContext context)
        {
            // If the Idempotency is disabled then stop
            if (!_enabled)
            {
                return;
            }

            // Stop if the PreIdempotency step is not applied:
            if (_idempotency == null)
            {
                return;
            }

            _idempotency.ApplyPostIdempotency(context);
        }
    }
}
