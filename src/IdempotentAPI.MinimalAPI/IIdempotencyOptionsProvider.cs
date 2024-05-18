using IdempotentAPI.Core;
using Microsoft.AspNetCore.Http;

namespace IdempotentAPI.MinimalAPI
{
    public interface IIdempotencyOptionsProvider
    {
        public IIdempotencyOptions GetIdempotencyOptions(IHttpContextAccessor httpContextAccessor);
    }
}