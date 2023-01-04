using System.Net;
using IdempotentAPI.AccessCache.Exceptions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace IdempotentAPI.Core;

public interface IResponseMapper
{
    public IActionResult ResultOnDistributedLockNotAcquired(ActionExecutingContext context,
        DistributedLockNotAcquiredException exception);

    public IActionResult CreateResponse(ActionExecutingContext context, HttpStatusCode status, object error);
    public IActionResult ResultOnMissingIdempotencyKeyHeader(ActionExecutingContext context, MissingIdempotencyKeyReason reason);
}