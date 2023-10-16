using Microsoft.AspNetCore.Http;

namespace IdempotentAPI.Extensions
{
    internal static class HttpResponseExtensions
    {
        public static bool IsSuccessStatusCode(this HttpResponse httpResponse)
        {
            if (httpResponse is null)
            {
                return false;
            }

            return httpResponse.StatusCode >= 200 && httpResponse.StatusCode < 300;
        }

    }
}
