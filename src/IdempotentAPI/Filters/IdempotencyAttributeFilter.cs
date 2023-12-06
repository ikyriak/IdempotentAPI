using System;
using System.Threading.Tasks;
using IdempotentAPI.AccessCache;
using IdempotentAPI.Core;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace IdempotentAPI.Filters
{
    public class IdempotencyAttributeFilter : IAsyncActionFilter, IAsyncResultFilter, IAsyncResourceFilter
    {
        private readonly bool _enabled;
        private readonly int _expireHours;
        private readonly string _headerKeyName;
        private readonly string _distributedCacheKeysPrefix;
        private readonly TimeSpan? _distributedLockTimeout;
        private readonly bool _cacheOnlySuccessResponses;
        private readonly bool _isIdempotencyOptional;
        private readonly IIdempotencyAccessCache _distributedCache;
        private readonly ILogger<Idempotency> _logger;

        private Idempotency? _idempotency = null;

        public IdempotencyAttributeFilter(
            IIdempotencyAccessCache distributedCache,
            ILoggerFactory loggerFactory,
            bool enabled,
            int expireHours,
            string headerKeyName,
            string distributedCacheKeysPrefix,
            TimeSpan? distributedLockTimeout,
            bool cacheOnlySuccessResponses,
            bool isIdempotencyOptional)
        {
            _distributedCache = distributedCache;
            _enabled = enabled;
            _expireHours = expireHours;
            _headerKeyName = headerKeyName;
            _distributedCacheKeysPrefix = distributedCacheKeysPrefix;
            _distributedLockTimeout = distributedLockTimeout;
            _cacheOnlySuccessResponses = cacheOnlySuccessResponses;
            _isIdempotencyOptional = isIdempotencyOptional;

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
        /// Runs before the model binding of the request (resource)
        /// </summary>
        /// <param name="context"></param>
        /// <param name="next"></param>
        public async Task OnResourceExecutionAsync(ResourceExecutingContext context, ResourceExecutionDelegate next)
        {
            // If the Idempotency is disabled then stop
            if (!_enabled)
            {
                await next();
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
                    _distributedLockTimeout,
                    _cacheOnlySuccessResponses,
                    _isIdempotencyOptional);
            }

            await _idempotency.PrepareIdempotency(context);

            await next();
        }


        /// <summary>
        /// Runs before the execution of the controller
        /// </summary>
        /// <param name="context"></param>
        /// <param name="next"></param>
        public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
        {
            // If the Idempotency is disabled then stop
            if (!_enabled)
            {
                await next();
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
                    _distributedLockTimeout,
                    _cacheOnlySuccessResponses,
                    _isIdempotencyOptional);
            }

            await _idempotency.ApplyPreIdempotency(context);

            // short-circuit to exit for async filter when result already set
            // https://learn.microsoft.com/en-us/aspnet/core/mvc/controllers/filters?view=aspnetcore-7.0#action-filters
            if (context.Result != null)
            {
                return;
            }

            var result = await next();
            if (result?.Exception is not null)
            {
                await _idempotency.CancelIdempotency();
            }
        }


        /// <summary>
        /// Runs after the creation of the response (result)
        /// </summary>
        /// <param name="context"></param>
        /// <param name="next"></param>
        public async Task OnResultExecutionAsync(ResultExecutingContext context, ResultExecutionDelegate next)
        {
            // If the Idempotency is disabled then stop
            if (!_enabled)
            {
                await next();
                return;
            }


            // Stop if the PreIdempotency step is not applied:
            if (_idempotency == null)
            {
                await next();
                return;
            }

            await next();

            await _idempotency.ApplyPostIdempotency(context);
        }
    }
}
