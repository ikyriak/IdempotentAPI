using System;

namespace IdempotentAPI.UnitTests.ApplicationServices.DTOs
{
    /// <summary>
    /// A basic serializable response model
    /// </summary>
    [Serializable]
    class ResponseModelBasic
    {
        public int Id { get; set; }
        public DateTime CreatedOn { get; set; }
    }
}
