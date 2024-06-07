using FastEndpoints;
using IdempotentAPI.MinimalAPI;
using IdempotentAPI.TestFastEndpointsAPIs.DTOs;

namespace IdempotentAPI.TestFastEndpointsAPIs.Endpoints
{
    public class TestingIdempotentAPI_TestObjectWithHttpError : Endpoint<RequestWithHttpError, ResponseDTOs?>
    {
        public override void Configure()
        {
            Post("/v6/TestingIdempotentAPI/testobjectWithHttpError");
            AllowAnonymous();
            Options(x => x.AddEndpointFilter<IdempotentAPIEndpointFilter>());
        }

        public override async Task HandleAsync(RequestWithHttpError requestWithHttpError, CancellationToken cancellationToken)
        {
            await Task.Delay(requestWithHttpError.DelaySeconds * 1000, cancellationToken);

            await SendAsync(null, requestWithHttpError.HttpErrorCode, cancellationToken);
        }
    }
}
