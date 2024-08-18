using System.Net;
using System.Security.Claims;
using IdempotentAPI.Cache.DistributedCache.Extensions.DependencyInjection;
using IdempotentAPI.Cache.FusionCache.Extensions.DependencyInjection;
using IdempotentAPI.DistributedAccessLock.MadelsonDistributedLock.Extensions.DependencyInjection;
using IdempotentAPI.DistributedAccessLock.RedLockNet.Extensions.DependencyInjection;
using IdempotentAPI.Extensions.DependencyInjection;
using IdempotentAPI.MinimalAPI;
using IdempotentAPI.MinimalAPI.Extensions.DependencyInjection;
using IdempotentAPI.TestWebMinimalAPIs;
using IdempotentAPI.TestWebMinimalAPIs.ApiContext;
using IdempotentAPI.TestWebMinimalAPIs.DTOs;
using Medallion.Threading;
using Medallion.Threading.Redis;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;
using StackExchange.Redis;
var builder = WebApplication.CreateBuilder(args);


builder.Services.AddIdempotentMinimalAPI(new IdempotencyOptionsProvider());

// FYI: The following commended code was replaced by the AddIdempotentMinimalAPI(...) extension above.
//builder.Services.AddIdempotentAPI();

//// This is REQUIRED for Minimal APIs to configure the Idempotency:
//builder.Services.AddSingleton<IIdempotencyOptions, IdempotencyOptions>();
//builder.Services.AddTransient(serviceProvider =>
//{
//    var distributedCache = serviceProvider.GetRequiredService<IIdempotencyAccessCache>();
//    var logger = serviceProvider.GetRequiredService<ILogger<Idempotency>>();
//    var idempotencyOptions = serviceProvider.GetRequiredService<IIdempotencyOptions>();

//    return new Idempotency(
//        distributedCache,
//        logger,
//        idempotencyOptions.ExpiresInMilliseconds,
//        idempotencyOptions.HeaderKeyName,
//        idempotencyOptions.DistributedCacheKeysPrefix,
//        TimeSpan.FromMilliseconds(idempotencyOptions.DistributedLockTimeoutMilli),
//        idempotencyOptions.CacheOnlySuccessResponses,
//        idempotencyOptions.IsIdempotencyOptional);
//});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(x =>
    x.SwaggerDoc("v6", new OpenApiInfo { Title = "IdempotentAPI.TestWebMinimalAPIs - Swagger", Version = "v6" }));

// The following dummy EF is used to test self-referencing loop when injecting the DbContext into the actions.
builder.Services
    .AddEntityFrameworkInMemoryDatabase()
    .AddDbContext<ApiDbContext>(opt => opt.UseInMemoryDatabase("TestDb"))
    .AddScoped<DbContext, ApiDbContext>();

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

app.MapPost("/v6/TestingIdempotentAPI/testobject", (
    [FromServices] DbContext dbContext,
    HttpRequest httpRequest,
    HttpContext context,
    HttpResponse response,
    ClaimsPrincipal user,
    CancellationToken cancellationToken
    ) =>
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
    (
    [FromServices] DbContext dbContext,
    [FromBody] RequestDTOs requestDTOs,
    HttpRequest httpRequest,
    HttpContext context,
    HttpResponse response,
    ClaimsPrincipal user,
    CancellationToken cancellationToken
    ) =>
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