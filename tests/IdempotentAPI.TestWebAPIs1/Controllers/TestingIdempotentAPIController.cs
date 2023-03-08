using System.Net;
using IdempotentAPI.Filters;
using IdempotentAPI.TestWebAPIs1.DTOs;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Primitives;

namespace IdempotentAPI.TestWebAPIs1.Controllers
{
    [ApiController]
    [ApiVersion("6.0")]
    [Route("v{version:apiVersion}/[controller]")]

    [Consumes("application/json")]
    [Produces("application/json")]

    [Idempotent(CacheOnlySuccessResponses = true, DistributedLockTimeoutMilli = 2000)]
    public class TestingIdempotentAPIController : ControllerBase
    {
        private readonly ILogger<TestingIdempotentAPIController> _logger;

        public TestingIdempotentAPIController(ILogger<TestingIdempotentAPIController> logger)
        {
            _logger = logger;
        }

        [HttpPost("test")]
        public ActionResult Test([FromHeader(Name = "IdempotencyKey")] string idempotencyKey)
        {
            return Ok(new ResponseDTOs());
        }

        [HttpPost("testobject")]
        public ResponseDTOs TestObject()
        {
            return new ResponseDTOs();
        }


        [HttpPost("testobjectWithHttpError")]
        public async Task<ActionResult> TestObjectWithHttpErrorAsync(
            [FromHeader(Name = "IdempotencyKey")] string idempotencyKey, int delaySeconds, int httpErrorCode)
        {
            await Task.Delay(delaySeconds * 1000);

            return StatusCode(httpErrorCode);
        }


        [HttpPost("testobjectWithException")]
        public async Task<ActionResult> TestObjectWithExceptionAsync(
            [FromHeader(Name = "IdempotencyKey")] string idempotencyKey, int delaySeconds)
        {
            await Task.Delay(delaySeconds * 1000);

            throw new Exception("Something when wrong!");
        }

        [HttpPost("customNotAcceptable406")]
        public async Task<ActionResult> TestCustomNotAcceptable406Async(
            [FromHeader(Name = "IdempotencyKey")] string idempotencyKey, int delaySeconds)
        {
            if (idempotencyKey is null)
            {
                throw new ArgumentNullException(nameof(idempotencyKey));
            }

            _logger.LogInformation($"Host: {Request.Host.Value} | IdempotencyKey: {idempotencyKey}");

            await Task.Delay(delaySeconds * 1000);

            string message = $"Not Acceptable! {DateTime.Now:s}";

            return new ObjectResult(new ErrorModel
            {
                Title = HttpStatusCode.NotAcceptable,
                StatusCode = StatusCodes.Status406NotAcceptable,
                Errors = new[]{
                    message
                },
            })
            {
                StatusCode = StatusCodes.Status406NotAcceptable,
            };
        }
    }
}