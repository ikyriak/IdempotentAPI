using IdempotentAPI.Core;
using IdempotentAPI.MinimalAPI;

namespace IdempotentAPI.TestWebMinimalAPIs
{
    /// <summary>
    /// WARNING: This example implementation shows that we can provide different IdempotencyOptions per case.
    /// </summary>
    public class IdempotencyOptionsProvider : IIdempotencyOptionsProvider
    {
        public IIdempotencyOptions GetIdempotencyOptions(IHttpContextAccessor httpContextAccessor)
        {
            switch (httpContextAccessor?.HttpContext?.Request.Path)
            {
                case "/v6/TestingIdempotentAPI/test":
                    return new IdempotencyOptions()
                    {
                        ExpireHours = 1,
                    };
            }

            return new IdempotencyOptions();
        }
    }
}
