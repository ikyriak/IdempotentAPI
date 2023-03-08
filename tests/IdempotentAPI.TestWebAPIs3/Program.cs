using System.Net;
using IdempotentAPI.Cache.DistributedCache.Extensions.DependencyInjection;
using IdempotentAPI.Extensions.DependencyInjection;
using IdempotentAPI.Filters;
using IdempotentAPI.TestWebAPIs1.DTOs;
using IdempotentAPI.TestWebAPIs3;
using IdempotentAPI.TestWebAPIs3.DTOs;
using Microsoft.AspNetCore.Mvc;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddIdempotentAPI();
builder.Services.AddDistributedMemoryCache();
builder.Services.AddIdempotentAPIUsingDistributedCache();

builder.Services.AddTransient(serviceProvider =>
{
    var idempotentAttribute = new IdempotentAttribute
    {
        CacheOnlySuccessResponses = true,
        DistributedLockTimeoutMilli = 2000
    };
    return (IdempotencyAttributeFilter)idempotentAttribute.CreateInstance(serviceProvider);
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(x =>
    x.SwaggerDoc("v6", new OpenApiInfo { Title = "IdempotentAPI.TestWebAPIs3 - Swagger", Version = "v6" }));

var app = builder.Build();

app
    .MapPost("/v6/TestingIdempotentAPI/test", 
        ([FromHeader(Name = "IdempotencyKey")] string idempotencyKey) => Results.Ok(new ResponseDTOs()))
    .AddEndpointFilter<IdempotentEndpointFilter>();

app
    .MapPost("/v6/TestingIdempotentAPI/testobject", 
        ([FromHeader(Name = "IdempotencyKey")] string idempotencyKey) => new ResponseDTOs())
    .AddEndpointFilter<IdempotentEndpointFilter>();

app
    .MapPost("/v6/TestingIdempotentAPI/testobjectWithHttpError", 
        async ([FromHeader(Name = "IdempotencyKey")] string idempotencyKey, int delaySeconds, int httpErrorCode) =>
        {
            await Task.Delay(delaySeconds * 1000);
            return Results.StatusCode(httpErrorCode);
        })
    .AddEndpointFilter<IdempotentEndpointFilter>();

app
    .MapPost("/v6/TestingIdempotentAPI/testobjectWithException", 
        async ([FromHeader(Name = "IdempotencyKey")] string idempotencyKey, int delaySeconds) =>
        {
            await Task.Delay(delaySeconds * 1000);
            throw new Exception("Something when wrong!");
        })
    .AddEndpointFilter<IdempotentEndpointFilter>();

app
    .MapPost("/v6/TestingIdempotentAPI/customNotAcceptable406", 
        async ([FromHeader(Name = "IdempotencyKey")] string idempotencyKey, int delaySeconds) =>
        {
            if (idempotencyKey is null)
            {
                throw new ArgumentNullException(nameof(idempotencyKey));
            }

            //TODO: Add support for logging
            //_logger.LogInformation($"Host: {Request.Host.Value} | IdempotencyKey: {idempotencyKey}");

            await Task.Delay(delaySeconds * 1000);

            string message = $"Not Acceptable! {DateTime.Now:s}";

            return Results.Json(new ErrorModel
            {
                Title = HttpStatusCode.NotAcceptable,
                StatusCode = StatusCodes.Status406NotAcceptable,
                Errors = new[]
                {
                    message
                }
            }, statusCode: StatusCodes.Status406NotAcceptable);
        })
    .AddEndpointFilter<IdempotentEndpointFilter>();

app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.DocumentTitle = "IdempotentAPI.TestWebAPIs3 - Swagger";
    c.RoutePrefix = string.Empty;
    c.SwaggerEndpoint("./swagger/v6/swagger.json", "v6");
});

app.Run();