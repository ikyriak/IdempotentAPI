using IdempotentAPI.Middlewares;
using Microsoft.AspNetCore.Builder;

namespace IdempotentAPI.Exceptions
{
    public static class IdempotencyMiddlewareExtensions
    {
        public static IApplicationBuilder UseIdempotency(this IApplicationBuilder builder, string policyName)
        {
            return builder.UseMiddleware<IdempotencyMiddleware>(policyName);
        }
    }
}
