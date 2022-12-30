using System;
using System.Linq;
using Microsoft.AspNetCore.Http;

namespace IdempotentAPI.Core;

public class DefaultRequestIdProvider : IRequestIdProvider
{
    private readonly IIdempotencySettings _settings;

    public DefaultRequestIdProvider(IIdempotencySettings settings)
    {
        _settings = settings;
    }

    public string Get(HttpRequest request)
    {
        request.Headers.TryGetValue(_settings.RequestIdHeader, out var value);
        value = value.FirstOrDefault() ?? Guid.NewGuid().ToString();
        return value.ToString();
    }
}