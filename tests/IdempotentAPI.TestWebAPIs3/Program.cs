using IdempotentAPI.Cache.DistributedCache.Extensions.DependencyInjection;
using IdempotentAPI.Extensions.DependencyInjection;
using IdempotentAPI.Filters;
using IdempotentAPI.TestWebAPIs3;
using Microsoft.AspNetCore.Mvc;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddIdempotentAPI();
builder.Services.AddDistributedMemoryCache();
builder.Services.AddIdempotentAPIUsingDistributedCache();

builder.Services.AddTransient(serviceProvider =>
{
    var idempotentAttribute = new IdempotentAttribute();
    return (IdempotencyAttributeFilter)idempotentAttribute.CreateInstance(serviceProvider);
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(x =>
    x.SwaggerDoc("v1", new OpenApiInfo { Title = "IdempotentAPI.TestWebAPIs3 - Swagger", Version = "v1" }));

var app = builder.Build();

app.MapPost("/api/",
        ([FromHeader] string idempotencyKey
        ) => new Response { Message = "Hello world", IdempotencyKey = idempotencyKey, Timestamp = DateTime.Now })
    .AddEndpointFilter<IdempotentEndpointFilter>();

app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.DocumentTitle = "IdempotentAPI.TestWebAPIs3 - Swagger";
    c.RoutePrefix = string.Empty;
    c.SwaggerEndpoint("./swagger/v1/swagger.json", "v1");
});

app.Run();