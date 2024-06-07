namespace IdempotentAPI.TestFastEndpointsAPIs.DTOs
{
    [Serializable]
    public class ResponseDTOs
    {
        public Guid Idempotency { get; set; } = Guid.NewGuid();
        public DateTime CreatedOn { get; set; } = DateTime.Now;
    }
}
