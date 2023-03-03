using IdempotentAPI.Filters;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;

namespace IdempotentAPI.TestWebAPIs3;

public class IdempotentEndpointFilter : IEndpointFilter
{
    private readonly IdempotencyAttributeFilter _idempotencyAttributeFilter;

    public IdempotentEndpointFilter(IdempotencyAttributeFilter idempotencyAttributeFilter)
    {
        _idempotencyAttributeFilter = idempotencyAttributeFilter;
    }
    
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var actionContext = new ActionContext(context.HttpContext, new RouteData(), new ActionDescriptor());
        var filters = new List<IFilterMetadata>();
        var actionArguments = new Dictionary<string, object?>();
        var actionExecutingContext = new ActionExecutingContext(actionContext, filters, actionArguments, null!);
        _idempotencyAttributeFilter.OnActionExecuting(actionExecutingContext);

        var actionExecutedContext = new ActionExecutedContext(actionContext, filters, null!);
        _idempotencyAttributeFilter.OnActionExecuted(actionExecutedContext);

        ObjectResult apiResult;
        if (actionExecutingContext.Result == null)
        {
            apiResult = new ObjectResult(await next(context));
        }
        else
        {
            apiResult = (ObjectResult)actionExecutingContext.Result;
        }
        
        var resultExecutingContext = new ResultExecutingContext(actionContext, filters, apiResult, null!);
        _idempotencyAttributeFilter.OnResultExecuting(resultExecutingContext);
        
        var resultExecutedContext = new ResultExecutedContext(actionContext, filters, apiResult, null!);
        _idempotencyAttributeFilter.OnResultExecuted(resultExecutedContext);

        return apiResult.Value;
    }
}