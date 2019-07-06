using Microsoft.AspNetCore.Builder;

namespace IdempotentAPI
{
    public static class IdempotencyMiddlewareExtensions
    {
        public static IApplicationBuilder UseIdempotency(this IApplicationBuilder builder, string policyName)
        {
            return builder.UseMiddleware<IdempotencyMiddleware>(policyName);
        }
    }
}
