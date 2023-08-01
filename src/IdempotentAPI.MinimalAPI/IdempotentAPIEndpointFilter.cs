using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using IdempotentAPI.AccessCache;
using IdempotentAPI.Core;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace IdempotentAPI.MinimalAPI;

public class IdempotentAPIEndpointFilter : IEndpointFilter
{
    private readonly IIdempotencyAccessCache _distributedCache;
    private readonly ILogger<Idempotency> _logger;
    private IIdempotencyOptions _idempotencyOptions;

    private Idempotency? _idempotency = null;


    public IdempotentAPIEndpointFilter(
        IIdempotencyAccessCache distributedCache,
        ILoggerFactory loggerFactory,
        IIdempotencyOptions idempotencyOptions)
    {
        _distributedCache = distributedCache;
        _idempotencyOptions = idempotencyOptions;

        if (loggerFactory != null)
        {
            _logger = loggerFactory.CreateLogger<Idempotency>();
        }
        else
        {
            _logger = NullLogger<Idempotency>.Instance;
        }
    }

    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        // Initialize only on its null (in case of multiple executions):
        if (_idempotency == null)
        {
            _idempotency = new Idempotency(
                _distributedCache,
                _logger,
                _idempotencyOptions.ExpireHours,
                _idempotencyOptions.HeaderKeyName,
                _idempotencyOptions.DistributedCacheKeysPrefix,
                TimeSpan.FromMilliseconds(_idempotencyOptions.DistributedLockTimeoutMilli),
                _idempotencyOptions.CacheOnlySuccessResponses);
        }

        try
        {
            bool applyPostIdempotency = true;
            var actionContext = new ActionContext(context.HttpContext, new RouteData(), new ActionDescriptor());
            var filters = new List<IFilterMetadata>();
            var actionArguments = new Dictionary<string, object?>();
            var actionExecutingContext = new ActionExecutingContext(actionContext, filters, actionArguments, null!);

            await _idempotency.ApplyPreIdempotency(actionExecutingContext);

            // short-circuit to exit for async filter when result already set
            // https://learn.microsoft.com/en-us/aspnet/core/mvc/controllers/filters?view=aspnetcore-7.0#action-filters
            if (actionExecutingContext.Result != null)
            {
                applyPostIdempotency = false;
            }

            // Execute the next EndpointFilter which eventually will execute the endpoint and we will get its results.
            ObjectResult objectResult;
            if (actionExecutingContext.Result == null)
            {
                var realCallResult = await next(context);

                object? value = string.Empty;
                if (realCallResult is not IResult)
                {
                    value = realCallResult;
                }
                else if (realCallResult is IValueHttpResult valueHttpResult)
                {
                    value = valueHttpResult.Value;
                }

                objectResult = new ObjectResult(value);

                if (realCallResult is IStatusCodeHttpResult statusCodeHttpResult &&
                    statusCodeHttpResult.StatusCode.HasValue)
                {
                    objectResult.StatusCode = statusCodeHttpResult.StatusCode.Value;
                }
            }
            else
            {
                object? value = string.Empty;
                if (actionExecutingContext.Result is ObjectResult valueHttpResult)
                {
                    value = valueHttpResult.Value;
                }

                objectResult = new ObjectResult(value);

                if (actionExecutingContext.Result is IStatusCodeActionResult statusCodeActionResult &&
                    statusCodeActionResult.StatusCode.HasValue)
                {
                    objectResult.StatusCode = statusCodeActionResult.StatusCode.Value;
                }
            }

            if (objectResult.StatusCode.HasValue)
            {
                context.HttpContext.Response.StatusCode = objectResult.StatusCode.Value;
            }

            if (!applyPostIdempotency)
            {
                return objectResult.Value;
            }

            var resultExecutingContext = new ResultExecutingContext(actionContext, filters, objectResult, null!);

            await _idempotency.ApplyPostIdempotency(resultExecutingContext);

            return objectResult.Value;
        }
        catch
        {
            await _idempotency.CancelIdempotency();
            throw;
        }

    }
}