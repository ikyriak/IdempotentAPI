using System;

namespace IdempotentAPI.UnitTests;

public class RequestDTOs
{
    public Guid Idempotency { get; set; } = Guid.NewGuid();
    public DateTime CreatedOn { get; set; } = DateTime.Now;
    public string Description { get; set; } = string.Empty;
    public int? Number { get; set; }
}