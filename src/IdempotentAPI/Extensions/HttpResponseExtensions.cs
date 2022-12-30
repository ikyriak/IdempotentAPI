using Microsoft.AspNetCore.Http;

namespace IdempotentAPI.Extensions
{
    internal static class HttpResponseExtensions
    {
        public static bool IsSuccessStatusCode(this HttpResponse? httpResponse)
        {
            return httpResponse?.StatusCode is >= 200 and < 300;
        }
    }
}
