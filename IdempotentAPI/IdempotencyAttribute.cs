using System;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Caching.Distributed;

//TODO: Change the Namespace to include the "IKyriakidis" as company name
namespace IdempotentAPI
{
    /// <summary>
    /// Use Idempotent operations on POST and PATCH http methods
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
    public class IdempotentAttribute : Attribute, IFilterFactory
    {
        public bool IsReusable => false;

        public bool Enabled { get; set; } = true;

        public int ExpireHours { get; set; } = 24;

        //TODO: Set a prefix for the distributedCache keys (with a default eg. IKIdemp_)

        public IFilterMetadata CreateInstance(IServiceProvider serviceProvider)
        {
            var distributedCache = (IDistributedCache)serviceProvider.GetService(typeof(IDistributedCache));

            IdempotencyAttributeFilter idempotencyAttributeFilter = new IdempotencyAttributeFilter(distributedCache, Enabled, ExpireHours);
            return idempotencyAttributeFilter;
        }
    }
}
