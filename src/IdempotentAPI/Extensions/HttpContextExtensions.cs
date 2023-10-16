using Microsoft.AspNetCore.Http;

namespace IdempotentAPI.Extensions
{
    internal static class HttpContextExtensions
    {
        private const string RequestsDataHashItemKey = "IdempotentAPIRequestsDataHash";

        public static string GetRequestsDataHash(this HttpContext httpContext)
        {
            if (httpContext.Items is null)
            {
                return string.Empty;
            }

            if (!httpContext.Items.ContainsKey(RequestsDataHashItemKey))
            {
                return string.Empty;
            }

            return (string)httpContext.Items[RequestsDataHashItemKey];
        }

        public static void SetRequestsDataHash(this HttpContext httpContext, string requestsDataHash)
        {
            httpContext.Items[RequestsDataHashItemKey] = requestsDataHash;
        }
    }
}
