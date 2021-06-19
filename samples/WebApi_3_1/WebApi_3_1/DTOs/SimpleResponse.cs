using System;

namespace WebApi_3_1.DTOs
{
    [Serializable]
    public class SimpleResponse
    {
        public int Id { get; set; }

        public string Message { get; set; }

        public DateTime CreatedOn { get; set; }
    }
}
