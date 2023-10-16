using System.IO;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;

namespace IdempotentAPI.Extensions
{
    internal static class HttpRequestExtensions
    {
        public static async Task<string?> GetRawBodyAsync(this HttpRequest request, Encoding? encoding = null)
        {
            if (!request.Body.CanRead)
            {
                return null;
            }

            // 2019-10-13: Use CanSeek to check if the stream does not support seeking (set position)
            // 2022-08-18: Enable buffering for large body requests and then read the buffer asynchronously.
            if (!request.Body.CanSeek)
            {
                request.EnableBuffering();
            }

            request.Body.Position = 0;

            var reader = new StreamReader(request.Body, encoding ?? Encoding.UTF8);

            var body = await reader.ReadToEndAsync().ConfigureAwait(false);

            request.Body.Position = 0;

            return body;
        }
    }
}
