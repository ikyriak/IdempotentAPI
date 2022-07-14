using IdempotentAPI.Cache;
using IdempotentAPI.Core;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace IdempotentAPI.Filters
{
    public class IdempotencyAttributeFilter : IActionFilter, IResultFilter
    {
        private readonly bool _enabled;
        private readonly int _expireHours;
        private readonly string _headerKeyName;
        private readonly string _distributedCacheKeysPrefix;
        private readonly bool _cacheOnlySuccessResponses;
        private readonly IIdempotencyCache _distributedCache;
        private readonly ILogger<Idempotency> _logger;

        private Idempotency? _idempotency = null;

        public IdempotencyAttributeFilter(
            IIdempotencyCache distributedCache,
            ILoggerFactory loggerFactory,
            bool enabled,
            int expireHours,
            string headerKeyName,
            string distributedCacheKeysPrefix,
            bool cacheOnlySuccessResponses = true)
        {
            _distributedCache = distributedCache;
            _enabled = enabled;
            _expireHours = expireHours;
            _headerKeyName = headerKeyName;
            _distributedCacheKeysPrefix = distributedCacheKeysPrefix;
            _cacheOnlySuccessResponses = cacheOnlySuccessResponses;

            if (loggerFactory != null)
            {
                _logger = loggerFactory.CreateLogger<Idempotency>();
            }
            else
            {
                _logger = NullLogger<Idempotency>.Instance;
            }
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
                    _expireHours,
                    _headerKeyName,
                    _distributedCacheKeysPrefix,
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
