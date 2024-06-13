using System.Net;
using FastEndpoints;
using IdempotentAPI.MinimalAPI;
using IdempotentAPI.TestFastEndpointsAPIs.DTOs;

namespace IdempotentAPI.TestFastEndpointsAPIs.Endpoints
{
    public class TestingIdempotentAPI_CustomNotAcceptable406 : Endpoint<RequestCustomNotAcceptable406, ErrorModel>
    {
        public override void Configure()
        {
            Post("/v6/TestingIdempotentAPI/customNotAcceptable406");
            AllowAnonymous();
            Options(x => x.AddEndpointFilter<IdempotentAPIEndpointFilter>());
        }

        public override async Task HandleAsync(RequestCustomNotAcceptable406 request, CancellationToken cancellationToken)
        {
            if (request.IdempotencyKey is null)
            {
                throw new ArgumentNullException(nameof(request.IdempotencyKey));
            }

            //TODO: Add support for logging
            //_logger.LogInformation($"Host: {Request.Host.Value} | IdempotencyKey: {idempotencyKey}");

            await Task.Delay(request.DelaySeconds * 1000, cancellationToken);

            string message = $"Not Acceptable! {DateTime.Now:s}";

            var errorModel = new ErrorModel
            {
                Title = HttpStatusCode.NotAcceptable,
                StatusCode = StatusCodes.Status406NotAcceptable,
                Errors = new[]
                {
                    message
                }
            };

            await SendAsync(errorModel, StatusCodes.Status406NotAcceptable, cancellation: cancellationToken);
        }
    }
}
