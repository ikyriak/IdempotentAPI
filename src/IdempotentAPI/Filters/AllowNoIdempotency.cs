using Microsoft.AspNetCore.Mvc.Filters;

namespace IdempotentAPI.Filters
{
    public sealed class AllowNoIdempotency : ActionFilterAttribute
    {
    }
}
