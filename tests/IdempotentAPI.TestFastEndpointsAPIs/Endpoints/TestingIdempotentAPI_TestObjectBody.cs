using FastEndpoints;
using IdempotentAPI.MinimalAPI;
using IdempotentAPI.TestFastEndpointsAPIs.DTOs;

namespace IdempotentAPI.TestFastEndpointsAPIs.Endpoints
{
    public class TestingIdempotentAPI_TestObjectBody : Endpoint<RequestDTOs, ResponseDTOs>
    {
        public override void Configure()
        {
            Post("/v6/TestingIdempotentAPI/testobjectbody");
            AllowAnonymous();
            Options(x => x.AddEndpointFilter<IdempotentAPIEndpointFilter>());
        }

        public override async Task HandleAsync(RequestDTOs requestDTOs, CancellationToken cancellationToken)
        {
            await SendAsync(new ResponseDTOs()
            {
                CreatedOn = requestDTOs.CreatedOn,
                Idempotency = requestDTOs.Idempotency,
            }, cancellation: cancellationToken);
        }
    }
}
