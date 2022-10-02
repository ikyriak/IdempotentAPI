using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace IdempotentAPI.TestWebAPIs1.DTOs
{
    [Serializable]
    public class ResponseDTOs
    {
        public Guid Idempotency { get; set; } = Guid.NewGuid();
        public DateTime CreatedOn { get; set; } = DateTime.Now;
    }
}
