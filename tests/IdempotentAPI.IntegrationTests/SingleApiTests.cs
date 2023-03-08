using System.Net;
using FluentAssertions;
using IdempotentAPI.IntegrationTests.Infrastructure;
using Xunit;

namespace IdempotentAPI.IntegrationTests;

/// <summary>
/// Used for testing a single API
/// NOTE: The API project needs to be running prior to running this test
/// </summary>

[Collection(nameof(CollectionFixture))]
public class SingleApiTests
{
    private readonly HttpClient[] _httpClients;

    public SingleApiTests(Fixture fixture)
    {
        _httpClients = new[]
        {
            fixture.Client1,
            fixture.Client2,
            fixture.Client3
        }
        ;
    }
    
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public async Task PostRequestsConcurrent_OnSameApi_WithErrorResponse_ShouldReturnTheErrorAndA409Response(int httpClientIndex)
    {
        // Arrange
        var guid = Guid.NewGuid().ToString();

        _httpClients[httpClientIndex].DefaultRequestHeaders.Add("IdempotencyKey", guid);

        // Act
        var httpPostTask1 = _httpClients[httpClientIndex].PostAsync("v6/TestingIdempotentAPI/customNotAcceptable406?delaySeconds=5", null);
        var httpPostTask2 = _httpClients[httpClientIndex].PostAsync("v6/TestingIdempotentAPI/customNotAcceptable406?delaySeconds=5", null);

        await Task.WhenAll(httpPostTask1, httpPostTask2);

        // Assert
        var resultStatusCodes = new List<HttpStatusCode>() { httpPostTask1.Result.StatusCode, httpPostTask2.Result.StatusCode };
        resultStatusCodes.Should().Contain(HttpStatusCode.NotAcceptable);
        resultStatusCodes.Should().Contain(HttpStatusCode.Conflict);
    }
    
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public async Task PostTest_ShouldReturnCachedResponse(int httpClientIndex)
    {
        // Arrange
        var guid = Guid.NewGuid().ToString();

        _httpClients[httpClientIndex].DefaultRequestHeaders.Add("IdempotencyKey", guid);

        // Act
        var response1 = await _httpClients[httpClientIndex].PostAsync("v6/TestingIdempotentAPI/test", null);
        var response2 = await _httpClients[httpClientIndex].PostAsync("v6/TestingIdempotentAPI/test", null);

        // Assert
        var content1 = await response1.Content.ReadAsStringAsync();
        var content2 = await response2.Content.ReadAsStringAsync();

        response1.StatusCode.Should().Be(HttpStatusCode.OK, content1);
        response2.StatusCode.Should().Be(HttpStatusCode.OK, content2);

        content1.Should().Be(content2);
    }
    
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public async Task PostTestObject_ShouldReturnCachedResponse(int httpClientIndex)
    {
        // Arrange
        var guid = Guid.NewGuid().ToString();

        _httpClients[httpClientIndex].DefaultRequestHeaders.Add("IdempotencyKey", guid);

        // Act
        var response1 = await _httpClients[httpClientIndex].PostAsync("v6/TestingIdempotentAPI/testobject", null);
        var response2 = await _httpClients[httpClientIndex].PostAsync("v6/TestingIdempotentAPI/testobject", null);

        // Assert
        var content1 = await response1.Content.ReadAsStringAsync();
        var content2 = await response2.Content.ReadAsStringAsync();

        response1.StatusCode.Should().Be(HttpStatusCode.OK, content1);
        response2.StatusCode.Should().Be(HttpStatusCode.OK, content2);

        content1.Should().Be(content2);
    }
}