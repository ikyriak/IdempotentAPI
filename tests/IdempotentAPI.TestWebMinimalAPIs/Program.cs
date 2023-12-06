using System.Net;
using IdempotentAPI.Cache.DistributedCache.Extensions.DependencyInjection;
using IdempotentAPI.Cache.FusionCache.Extensions.DependencyInjection;
using IdempotentAPI.Core;
using IdempotentAPI.DistributedAccessLock.MadelsonDistributedLock.Extensions.DependencyInjection;
using IdempotentAPI.DistributedAccessLock.RedLockNet.Extensions.DependencyInjection;
using IdempotentAPI.Extensions.DependencyInjection;
using IdempotentAPI.MinimalAPI;
using IdempotentAPI.TestWebMinimalAPIs.DTOs;
using Medallion.Threading;
using Medallion.Threading.Redis;
using Microsoft.AspNetCore.Mvc;
using Microsoft.OpenApi.Models;
using StackExchange.Redis;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddIdempotentAPI();

// This is REQUIRED for Minimal APIs to configure the Idempotency:
builder.Services.AddSingleton<IIdempotencyOptions, IdempotencyOptions>();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(x =>
    x.SwaggerDoc("v6", new OpenApiInfo { Title = "IdempotentAPI.TestWebAPIs3 - Swagger", Version = "v6" }));


// TODO: Hard-code the "Caching" and "Distributed Access Lock" methods until the following issue is resolved.
// Issue: https://github.com/dotnet/aspnetcore/issues/37680

// Register the Caching Method:
var caching = builder.Configuration.GetValue<string>("Caching") ?? "FusionCache";
switch (caching)
{
    case "MemoryCache":
        builder.Services.AddDistributedMemoryCache();
        builder.Services.AddIdempotentAPIUsingDistributedCache();
        break;
    // Caching: FusionCache(via Redis)
    case "FusionCache":
        builder.Services.AddStackExchangeRedisCache(options =>
        {
            options.Configuration = "localhost:6379";
        });
        builder.Services.AddFusionCacheNewtonsoftJsonSerializer();
        builder.Services.AddIdempotentAPIUsingFusionCache();
        break;
    default:
        Console.WriteLine($"Caching method '{caching}' is not recognized. Options: MemoryCache, FusionCache.");
        Environment.Exit(0);
        break;
}
Console.WriteLine($"Caching method: {caching}");



// Register the Distributed Access Lock Method:
var distributedAccessLock = builder.Configuration.GetValue<string>("DALock") ?? "MadelsonDistLock";
switch (distributedAccessLock)
{
    // RedLock.Net
    case "RedLockNet":
        List<DnsEndPoint> redisEndpoints = new List<DnsEndPoint>()
                    {
                        new DnsEndPoint("localhost", 6379)
                    };
        builder.Services.AddRedLockNetDistributedAccessLock(redisEndpoints);
        break;
    // Madelson/DistributedLock (via Redis)
    case "MadelsonDistLock":
        var redicConnection = ConnectionMultiplexer.Connect("localhost:6379");
        builder.Services.AddSingleton<IDistributedLockProvider>(_ => new RedisDistributedSynchronizationProvider(redicConnection.GetDatabase()));
        builder.Services.AddMadelsonDistributedAccessLock();
        break;
    case "None":
        Console.WriteLine("No distributed cache will be used.");
        break;
    default:
        Console.WriteLine($"Distributed Access Lock Method '{distributedAccessLock}' is not recognized. Options: RedLockNet, MadelsonDistLock.");
        Environment.Exit(0);
        break;
}
Console.WriteLine($"Distributed Access Lock Method: {distributedAccessLock}");



var app = builder.Build();

app.MapPost("/v6/TestingIdempotentAPI/test", () =>
    {
        return Results.Ok(new ResponseDTOs());
    })
    .AddEndpointFilter<IdempotentAPIEndpointFilter>();

app.MapPost("/v6/TestingIdempotentAPI/testobject", () =>
    {
        return new ResponseDTOs();
    })
    .AddEndpointFilter<IdempotentAPIEndpointFilter>();

app.MapPost("/v6/TestingIdempotentOptionalAPI/test", () =>
    {
        return Results.Ok(new ResponseDTOs());
    })
    .AddEndpointFilter<IdempotentAPIEndpointFilter>();

app.MapPost("/v6/TestingIdempotentOptionalAPI/testobject", () =>
    {
        return new ResponseDTOs();
    })
    .AddEndpointFilter<IdempotentAPIEndpointFilter>();

app.MapPost("/v6/TestingIdempotentAPI/testobjectbody",
    ([FromBody] RequestDTOs requestDTOs) =>
    {
        return new ResponseDTOs()
        {
            CreatedOn = requestDTOs.CreatedOn,
            Idempotency = requestDTOs.Idempotency,
        };
    })
    .AddEndpointFilter<IdempotentAPIEndpointFilter>();

app.MapPost("/v6/TestingIdempotentAPI/testobjectWithHttpError",
    async (int delaySeconds, int httpErrorCode) =>
    {
        await Task.Delay(delaySeconds * 1000);
        return Results.StatusCode(httpErrorCode);
    })
    .AddEndpointFilter<IdempotentAPIEndpointFilter>();

app.MapPost("/v6/TestingIdempotentAPI/testobjectWithException",
    async (int delaySeconds) =>
    {
        await Task.Delay(delaySeconds * 1000);
        throw new Exception("Something when wrong!");
    })
    .AddEndpointFilter<IdempotentAPIEndpointFilter>();

app.MapPost("/v6/TestingIdempotentAPI/customNotAcceptable406",
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
    .AddEndpointFilter<IdempotentAPIEndpointFilter>();

app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.DocumentTitle = "IdempotentAPI.WebMinimalAPIs - Swagger";
    c.RoutePrefix = string.Empty;
    c.SwaggerEndpoint("./swagger/v6/swagger.json", "v6");
});

app.Run();

public partial class Program { }