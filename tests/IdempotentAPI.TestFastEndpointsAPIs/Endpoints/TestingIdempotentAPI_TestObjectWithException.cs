using FastEndpoints;
using IdempotentAPI.MinimalAPI;
using IdempotentAPI.TestFastEndpointsAPIs.DTOs;

namespace IdempotentAPI.TestFastEndpointsAPIs.Endpoints
{
    public class TestingIdempotentAPI_TestObjectWithException : Endpoint<RequestWithHttpException, ResponseDTOs>
    {
        public override void Configure()
        {
            Post("/v6/TestingIdempotentAPI/testobjectWithException");
            AllowAnonymous();
            Options(x => x.AddEndpointFilter<IdempotentAPIEndpointFilter>());
        }

        public override async Task HandleAsync(RequestWithHttpException requestWithHttpException, CancellationToken cancellationToken)
        {
            await Task.Delay(requestWithHttpException.DelaySeconds * 1000, cancellationToken);
            throw new Exception("Something when wrong!");
        }
    }
}
