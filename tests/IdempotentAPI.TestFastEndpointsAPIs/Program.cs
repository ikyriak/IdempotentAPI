using System.Net;
using FastEndpoints;
using FastEndpoints.Swagger;
using IdempotentAPI.Cache.DistributedCache.Extensions.DependencyInjection;
using IdempotentAPI.Cache.FusionCache.Extensions.DependencyInjection;
using IdempotentAPI.Core;
using IdempotentAPI.DistributedAccessLock.MadelsonDistributedLock.Extensions.DependencyInjection;
using IdempotentAPI.DistributedAccessLock.RedLockNet.Extensions.DependencyInjection;
using IdempotentAPI.Extensions.DependencyInjection;
using Medallion.Threading;
using Medallion.Threading.Redis;
using Microsoft.OpenApi.Models;
using StackExchange.Redis;

var builder = WebApplication.CreateBuilder(args);
builder.Services
    .AddFastEndpoints()
    .SwaggerDocument(o =>
    {
        o.DocumentSettings = s =>
        {
            s.Title = "IdempotentAPI.TestFastEndpointsAPIs - Swagger";
        };
    });


builder.Services.AddIdempotentMinimalAPI(new IdempotencyOptions());

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(x =>
    x.SwaggerDoc("v6", new OpenApiInfo { Title = "IdempotentAPI.TestFastEndpointsAPIs - Swagger", Version = "v6" }));


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
app.UseFastEndpoints()
   .UseSwaggerGen();

app.Run();

public partial class Program { }