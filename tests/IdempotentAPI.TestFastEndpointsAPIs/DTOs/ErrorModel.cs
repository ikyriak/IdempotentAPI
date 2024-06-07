using System.Net;

namespace IdempotentAPI.TestFastEndpointsAPIs.DTOs
{
    public class ErrorModel
    {
        public HttpStatusCode Title { get; set; }
        public int StatusCode { get; set; }
        public string[]? Errors { get; set; }
    }
}
