using System.Collections.Generic;
using System.Security.Cryptography;
using IdempotentAPI.Core;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Internal;
using Xunit;

namespace IdempotentAPI.UnitTests;

public class IdempotencyTest
{
    [Fact]
    public void GenerateRequestsDataHashMinimalApi_ShouldGenerateDifferentHash_OnDifferentPayloads()
    {
        var requestDTO1 = new RequestDTOs { Description = "A request body." };
        var requestDTO2 = new RequestDTOs { Description = "A different request body with the same IdempotencyKey." };

        var hash1 = Idempotency.GenerateRequestsDataHashMinimalApi(
            new List<object> { requestDTO1 },
            new DefaultHttpRequest(new DefaultHttpContext()), SHA256.Create());
        
        var hash2 = Idempotency.GenerateRequestsDataHashMinimalApi(
            new List<object> { requestDTO2 },
            new DefaultHttpRequest(new DefaultHttpContext()), SHA256.Create());
        
        Assert.NotEmpty(hash1);
        Assert.NotEmpty(hash2);
        Assert.NotEqual(hash1, hash2);
    }
}