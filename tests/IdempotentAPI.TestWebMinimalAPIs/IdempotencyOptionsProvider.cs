using IdempotentAPI.Core;
using IdempotentAPI.MinimalAPI;
using Microsoft.EntityFrameworkCore;

namespace IdempotentAPI.TestWebMinimalAPIs
{
    /// <summary>
    /// WARNING: This example implementation shows that we can provide different IdempotencyOptions per case.
    /// </summary>
    public class IdempotencyOptionsProvider : IIdempotencyOptionsProvider
    {
        private readonly List<Type> ExcludeRequestSpecialTypes = new()
        {
            typeof(DbContext),
        };

        public IIdempotencyOptions GetIdempotencyOptions(IHttpContextAccessor httpContextAccessor)
        {
            switch (httpContextAccessor?.HttpContext?.Request.Path)
            {
                case "/v6/TestingIdempotentAPI/test":
                    return new IdempotencyOptions()
                    {
                        ExpireHours = 1,
                        ExcludeRequestSpecialTypes = ExcludeRequestSpecialTypes,
                    };
            }

            return new IdempotencyOptions()
            {
                ExcludeRequestSpecialTypes = ExcludeRequestSpecialTypes,
            };
        }
    }
}
