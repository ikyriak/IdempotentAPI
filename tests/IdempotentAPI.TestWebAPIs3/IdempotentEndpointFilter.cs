﻿using System.Net;
using IdempotentAPI.Filters;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.Infrastructure;

namespace IdempotentAPI.TestWebAPIs3;

public class IdempotentEndpointFilter : IEndpointFilter
{
    private readonly IdempotencyAttributeFilter _idempotencyAttributeFilter;
    private readonly ILogger<IdempotentEndpointFilter> _logger;

    public IdempotentEndpointFilter(IdempotencyAttributeFilter idempotencyAttributeFilter, ILogger<IdempotentEndpointFilter> logger)
    {
        _idempotencyAttributeFilter = idempotencyAttributeFilter;
        _logger = logger;
    }
    
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        try
        {
            var actionContext = new ActionContext(context.HttpContext, new RouteData(), new ActionDescriptor());
            var filters = new List<IFilterMetadata>();
            var actionArguments = new Dictionary<string, object?>();
            var actionExecutingContext = new ActionExecutingContext(actionContext, filters, actionArguments, null!);
            _idempotencyAttributeFilter.OnActionExecuting(actionExecutingContext);

            var actionExecutedContext = new ActionExecutedContext(actionContext, filters, null!);
            _idempotencyAttributeFilter.OnActionExecuted(actionExecutedContext);

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

            var resultExecutingContext = new ResultExecutingContext(actionContext, filters, objectResult, null!);
            _idempotencyAttributeFilter.OnResultExecuting(resultExecutingContext);

            var resultExecutedContext = new ResultExecutedContext(actionContext, filters, objectResult, null!);
            _idempotencyAttributeFilter.OnResultExecuted(resultExecutedContext);

            return objectResult.Value;
        }
        catch (ArgumentNullException argumentNullException)
        {
            if (argumentNullException.ParamName == "IdempotencyKey")
            {
                context.HttpContext.Response.StatusCode = (int)HttpStatusCode.BadRequest;
            }
            else
            {
                context.HttpContext.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
            }
            
            _logger.LogError(argumentNullException, "Idempotency error");

            return argumentNullException.ToString();
        }
        catch (Exception exception)
        {
            context.HttpContext.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
            
            _logger.LogError(exception, "Idempotency error");
            
            return exception.ToString();
        }
    }
}