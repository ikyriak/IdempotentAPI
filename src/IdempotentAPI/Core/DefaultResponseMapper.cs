using System.Net;
using IdempotentAPI.AccessCache.Exceptions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace IdempotentAPI.Core;

public class DefaultResponseMapper : IResponseMapper
{
    public IActionResult ResultOnDistributedLockNotAcquired(ActionExecutingContext context, DistributedLockNotAcquiredException exception)
    {
        return new ConflictResult();
    }

    public IActionResult CreateResponse(ActionExecutingContext context, HttpStatusCode status, object error) =>
        status switch
        {
            HttpStatusCode.Conflict => new ConflictObjectResult(error),
            HttpStatusCode.BadRequest => new BadRequestObjectResult(error),
            _ => new StatusCodeResult((int)status)
        };

    public IActionResult ResultOnMissingIdempotencyKeyHeader(ActionExecutingContext context, MissingIdempotencyKeyReason reason)
    {
        return new BadRequestResult();
    }
}