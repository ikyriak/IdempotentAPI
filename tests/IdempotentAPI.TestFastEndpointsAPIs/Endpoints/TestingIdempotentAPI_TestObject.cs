using FastEndpoints;
using IdempotentAPI.MinimalAPI;
using IdempotentAPI.TestFastEndpointsAPIs.DTOs;

namespace IdempotentAPI.TestFastEndpointsAPIs.Endpoints
{
    public class TestingIdempotentAPI_TestObject : EndpointWithoutRequest<ResponseDTOs>
    {
        public override void Configure()
        {
            Post("/v6/TestingIdempotentAPI/testobject");
            AllowAnonymous();
            Options(x => x.AddEndpointFilter<IdempotentAPIEndpointFilter>());
        }

        public override async Task HandleAsync(CancellationToken cancellationToken)
        {
            await SendAsync(new ResponseDTOs(), cancellation: cancellationToken);
        }
    }
}
