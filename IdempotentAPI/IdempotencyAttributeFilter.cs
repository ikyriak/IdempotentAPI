using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Caching.Distributed;

namespace IdempotentAPI
{
    public class IdempotencyAttributeFilter : IActionFilter, IResultFilter
    {
        public bool Enabled { get; set; }
        public int ExpireHours { get; set; }


        private Idempotency _idempotency { get; set; } = null;


        private readonly IDistributedCache _distributedCache;

        public IdempotencyAttributeFilter(IDistributedCache distributedCache, bool Enabled, int ExpireHours)
        {
            _distributedCache = distributedCache;
            this.Enabled = Enabled;
            this.ExpireHours = ExpireHours;
        }

        /// <summary>
        /// Runs before the execution of the controller
        /// </summary>
        /// <param name="context"></param>
        public void OnActionExecuting(ActionExecutingContext context)
        {
            // If the Idempotenc is disabled then stop
            if (!Enabled)
            {
                return;
            }

            _idempotency = new Idempotency(_distributedCache, ExpireHours);
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
            // If the Idempotenc is disabled then stop
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
