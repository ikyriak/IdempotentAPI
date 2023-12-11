using IdempotentAPI.Filters;
using IdempotentAPI.TestWebAPIs.DTOs;
using Microsoft.AspNetCore.Mvc;

namespace IdempotentAPI.TestWebAPIs.Controllers
{
    [ApiController]
    [ApiVersion("6.0")]
    [Route("v{version:apiVersion}/[controller]")]

    [Consumes("application/json")]
    [Produces("application/json")]

    [Idempotent(CacheOnlySuccessResponses = true, DistributedLockTimeoutMilli = 2000, IsIdempotencyOptional = true, ExpiresInMilliseconds = (1000 * 60 * 60))]
    public class TestingIdempotentOptionalAPIController : ControllerBase
    {
        private readonly ILogger<TestingIdempotentOptionalAPIController> _logger;

        public TestingIdempotentOptionalAPIController(ILogger<TestingIdempotentOptionalAPIController> logger)
        {
            _logger = logger;
        }

        [HttpPost("test")]
        public ActionResult Test()
        {
            return Ok(new ResponseDTOs());
        }

        [HttpPost("testobject")]
        public ResponseDTOs TestObject()
        {
            return new ResponseDTOs();
        }
    }
}