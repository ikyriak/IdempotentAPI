using NodaTime;

namespace IdempotentAPI.TestWebAPIs.DTOs
{
    [Serializable]
    public class ResponseDTOs
    {
        public Guid Idempotency { get; set; } = Guid.NewGuid();
        public DateTime CreatedOn { get; set; } = DateTime.Now;
        public Instant CreatedOnNodaTime { get; set; } = Instant.FromDateTimeUtc(DateTime.UtcNow);
    }
}
