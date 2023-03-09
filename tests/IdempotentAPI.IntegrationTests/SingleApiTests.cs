using System.Net;
using FluentAssertions;
using IdempotentAPI.IntegrationTests.Infrastructure;
using Xunit;
using Xunit.Abstractions;

namespace IdempotentAPI.IntegrationTests;

/// <summary>
/// Used for testing a single API
/// NOTE: The API project needs to be running prior to running this test
/// </summary>
[Collection(nameof(CollectionFixture))]
public class SingleApiTests
{
    private readonly ITestOutputHelper _testOutputHelper;
    private readonly HttpClient[] _httpClients;

    public SingleApiTests(Fixture fixture, ITestOutputHelper testOutputHelper)
    {
        _testOutputHelper = testOutputHelper;
        _httpClients = new[]
            {
                fixture.TestServerClient1,
                fixture.TestServerClient2,
                fixture.TestServerClient3
            };

        foreach (var httpClient in _httpClients)
        {
            httpClient.DefaultRequestHeaders.Clear();
        }
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
        _testOutputHelper.WriteLine($"content1: {Environment.NewLine}{content1}");
        _testOutputHelper.WriteLine($"content2: {Environment.NewLine}{content2}");

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
        _testOutputHelper.WriteLine($"content1: {Environment.NewLine}{content1}");
        _testOutputHelper.WriteLine($"content2: {Environment.NewLine}{content2}");

        response1.StatusCode.Should().Be(HttpStatusCode.OK, content1);
        response2.StatusCode.Should().Be(HttpStatusCode.OK, content2);

        content1.Should().NotBeNull();
        content1.Should().NotBe("null");
        content1.Should().Be(content2);
    }
    
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public async Task PostTestObjectWithHttpError_ShouldReturnExpectedStatusCode_NotCaching(int httpClientIndex)
    {
        // Arrange
        var guid = Guid.NewGuid().ToString();

        _httpClients[httpClientIndex].DefaultRequestHeaders.Add("IdempotencyKey", guid);

        // Act
        const HttpStatusCode expectedhttpStatusCode = HttpStatusCode.BadGateway;
        const int delaySeconds = 1;
        var response1 = await _httpClients[httpClientIndex].PostAsync($"v6/TestingIdempotentAPI/testobjectWithHttpError?delaySeconds={delaySeconds}&httpErrorCode={(int)expectedhttpStatusCode}", null);
        var response2 = await _httpClients[httpClientIndex].PostAsync($"v6/TestingIdempotentAPI/testobjectWithHttpError?delaySeconds={delaySeconds}&httpErrorCode={(int)expectedhttpStatusCode}", null);

        // Assert
        var content1 = await response1.Content.ReadAsStringAsync();
        var content2 = await response2.Content.ReadAsStringAsync();
        _testOutputHelper.WriteLine($"content1: {Environment.NewLine}{content1}");
        _testOutputHelper.WriteLine($"content2: {Environment.NewLine}{content2}");

        response1.StatusCode.Should().Be(expectedhttpStatusCode, content1);
        response2.StatusCode.Should().Be(expectedhttpStatusCode, content2);
    }
    
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public async Task PostTestObjectWithHttpError_ShouldReturnExpectedStatusCode_Cached(int httpClientIndex)
    {
        // Arrange
        var guid = Guid.NewGuid().ToString();

        _httpClients[httpClientIndex].DefaultRequestHeaders.Add("IdempotencyKey", guid);

        // Act
        const HttpStatusCode expectedhttpStatusCode = HttpStatusCode.Created;
        const int delaySeconds = 1;
        var response1 = await _httpClients[httpClientIndex].PostAsync($"v6/TestingIdempotentAPI/testobjectWithHttpError?delaySeconds={delaySeconds}&httpErrorCode={(int)expectedhttpStatusCode}", null);
        var response2 = await _httpClients[httpClientIndex].PostAsync($"v6/TestingIdempotentAPI/testobjectWithHttpError?delaySeconds={delaySeconds}&httpErrorCode={(int)expectedhttpStatusCode}", null);

        // Assert
        var content1 = await response1.Content.ReadAsStringAsync();
        var content2 = await response2.Content.ReadAsStringAsync();
        _testOutputHelper.WriteLine($"content1: {Environment.NewLine}{content1}");
        _testOutputHelper.WriteLine($"content2: {Environment.NewLine}{content2}");

        response1.StatusCode.Should().Be(expectedhttpStatusCode, content1);
        response2.StatusCode.Should().Be(expectedhttpStatusCode, content2);
        
        content1.Should().Be(string.Empty);
        content2.Should().Be(string.Empty);
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
        const int delaySeconds = 1;
        var httpPostTask1 = _httpClients[httpClientIndex]
            .PostAsync($"v6/TestingIdempotentAPI/customNotAcceptable406?delaySeconds={delaySeconds}", null);
        var httpPostTask2 = _httpClients[httpClientIndex]
            .PostAsync($"v6/TestingIdempotentAPI/customNotAcceptable406?delaySeconds={delaySeconds}", null);

        await Task.WhenAll(httpPostTask1, httpPostTask2);

        var content1 = await httpPostTask1.Result.Content.ReadAsStringAsync();
        var content2 = await httpPostTask2.Result.Content.ReadAsStringAsync();
        _testOutputHelper.WriteLine($"content1: {Environment.NewLine}{content1}");
        _testOutputHelper.WriteLine($"content2: {Environment.NewLine}{content2}");
        
        // Assert
        var resultStatusCodes = new List<HttpStatusCode>
        {
            httpPostTask1.Result.StatusCode,
            httpPostTask2.Result.StatusCode
        };
        resultStatusCodes.Should().Contain(HttpStatusCode.NotAcceptable);
        resultStatusCodes.Should().Contain(HttpStatusCode.Conflict);
    }
    
    
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public async Task Post_DifferentEndpoints_SameIdemptotentKey_ShouldReturnFailure(int httpClientIndex)
    {
        // Arrange
        var guid = Guid.NewGuid().ToString();

        _httpClients[httpClientIndex].DefaultRequestHeaders.Add("IdempotencyKey", guid);

        // Act
        var response1 = await _httpClients[httpClientIndex].PostAsync("v6/TestingIdempotentAPI/test", null);
        var response2 = await _httpClients[httpClientIndex].PostAsync("v6/TestingIdempotentAPI/testobject", null);

        // Assert
        var content1 = await response1.Content.ReadAsStringAsync();
        var content2 = await response2.Content.ReadAsStringAsync();
        _testOutputHelper.WriteLine($"content1: {Environment.NewLine}{content1}");
        _testOutputHelper.WriteLine($"content2: {Environment.NewLine}{content2}");

        response1.StatusCode.Should().Be(HttpStatusCode.OK, content1);
        response2.StatusCode.Should().Be(HttpStatusCode.BadRequest, content2);
    }
}