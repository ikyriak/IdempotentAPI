using IdempotentAPI.Core;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace IdempotentAPI.Filters
{
    public class IdempotencyAttributeFilter : IActionFilter, IResultFilter
    {
        public bool Enabled { get; private set; }
        public int ExpireHours { get; private set; }
        public string HeaderKeyName { get; private set; }
        public string DistributedCacheKeysPrefix { get; private set; }


        private Idempotency _idempotency { get; set; } = null;


        private readonly IDistributedCache _distributedCache;
        private readonly ILogger _logger;

        public IdempotencyAttributeFilter(
            IDistributedCache distributedCache,
            ILoggerFactory loggerFactory,
            bool Enabled,
            int ExpireHours,
            string HeaderKeyName,
            string DistributedCacheKeysPrefix)
        {
            _distributedCache = distributedCache;
            this.Enabled = Enabled;
            this.ExpireHours = ExpireHours;
            this.HeaderKeyName = HeaderKeyName;
            this.DistributedCacheKeysPrefix = DistributedCacheKeysPrefix;

            if (loggerFactory != null)
            {
                _logger = loggerFactory.CreateLogger<IdempotencyAttributeFilter>();
            }
            else
            {
                _logger = NullLogger.Instance;
            }
        }

        /// <summary>
        /// Runs before the execution of the controller
        /// </summary>
        /// <param name="context"></param>
        public void OnActionExecuting(ActionExecutingContext context)
        {
            // If the Idempotency is disabled then stop
            if (!Enabled)
            {
                return;
            }

            // Initialize only on its null (in case of multiple executions):
            if (_idempotency == null)
            {
                _idempotency = new Idempotency(_distributedCache, _logger, ExpireHours, HeaderKeyName, DistributedCacheKeysPrefix);
            }

            _idempotency.ApplyPreIdempotency(context);
        }

        // NOT USED
        public void OnActionExecuted(ActionExecutedContext context)
        {

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
            if (!Enabled)
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
