using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;


namespace IdempotentAPI
{
    public class IdempotencyMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly string _policyName;

        public IdempotencyMiddleware(RequestDelegate next, string policyName)
        {
            _next = next;
            _policyName = policyName;
        }

        public async Task Invoke(HttpContext httpContext)
        {
            Console.WriteLine($"IdempotencyMiddleware: Request for {httpContext.Request.Method}: {httpContext.Request.Path} received ({httpContext.Request.ContentLength ?? 0} bytes)");

            // Console.WriteLine($"IdempotencyMiddleware: Type: {this.GetType().ToString()}");




            // Call the next middleware delegate in the pipeline 
            await _next.Invoke(httpContext);
        }
    }

}
