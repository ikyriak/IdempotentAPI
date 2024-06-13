using FastEndpoints;

namespace IdempotentAPI.TestFastEndpointsAPIs.DTOs
{
    public class RequestCustomNotAcceptable406
    {
        [FromHeader("IdempotencyKey")]
        public string? IdempotencyKey { get; set; }
        public int DelaySeconds { get; set; }
    }
}
