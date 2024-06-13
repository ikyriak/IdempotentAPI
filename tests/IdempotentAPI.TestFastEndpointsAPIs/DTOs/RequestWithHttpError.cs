namespace IdempotentAPI.TestFastEndpointsAPIs.DTOs
{
    public class RequestWithHttpError
    {
        public int DelaySeconds { get; set; }
        public int HttpErrorCode { get; set; }
    }
}
