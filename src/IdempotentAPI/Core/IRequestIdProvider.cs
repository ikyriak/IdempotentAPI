using Microsoft.AspNetCore.Http;

namespace IdempotentAPI.Core;

public interface IRequestIdProvider
{
    string Get(HttpRequest request);
}