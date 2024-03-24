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

    public class TestingIdempotentAPIPerMethodController : ControllerBase
    {
        private readonly ILogger<TestingIdempotentAPIController> _logger;

        public TestingIdempotentAPIPerMethodController(ILogger<TestingIdempotentAPIController> logger)
        {
            _logger = logger;
        }

        [HttpPost("testUseIdempotencyOption")]
        [Idempotent(UseIdempotencyOption = true)]
        public ActionResult TestUseIdempotencyOption()
        {
            return Ok(new ResponseDTOs());
        }
    }
}