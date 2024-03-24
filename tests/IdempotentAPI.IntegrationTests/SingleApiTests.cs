using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using IdempotentAPI.TestWebAPIs.DTOs;
using Xunit;
using Xunit.Abstractions;

namespace IdempotentAPI.IntegrationTests;

/// <summary>
/// Used for testing a single API
/// NOTE: The API project needs to be running prior to running this test
/// </summary>
public class SingleApiTests : IClassFixture<WebApi1ApplicationFactory>, IClassFixture<WebMinimalApi1ApplicationFactory>
{
    private readonly ITestOutputHelper _testOutputHelper;
    private readonly HttpClient[] _httpClients;

    private const int WebApiClientIndex = 0;
    private const int WebMinimalApiClientIndex = 1;

    public SingleApiTests(
        WebApi1ApplicationFactory api1ApplicationFactory,
        WebMinimalApi1ApplicationFactory minimalApi1ApplicationFactory,
        ITestOutputHelper testOutputHelper)
    {
        _testOutputHelper = testOutputHelper;
        _httpClients = new[]
            {
                api1ApplicationFactory.CreateClient(),
                minimalApi1ApplicationFactory.CreateClient(),
            };
    }

    [Theory]
    [InlineData(WebApiClientIndex)]
    [InlineData(WebMinimalApiClientIndex)]
    public async Task PostTest_ShouldReturnCachedResponse(int httpClientIndex)
    {
        // Arrange
        var guid = Guid.NewGuid().ToString();

        _httpClients[httpClientIndex].DefaultRequestHeaders.Clear();
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


    [Fact]
    public async Task PostTest_WhenUsingIdempotencyOptionOnWebApiClient_ShouldReturnCachedResponse()
    {
        // Arrange
        var guid = Guid.NewGuid().ToString();

        int httpClientIndex = WebApiClientIndex;
        _httpClients[httpClientIndex].DefaultRequestHeaders.Clear();
        _httpClients[httpClientIndex].DefaultRequestHeaders.Add("IdempotencyKey", guid);

        // Act
        var response1 = await _httpClients[httpClientIndex].PostAsync("v6/TestingIdempotentAPIPerMethod/testUseIdempotencyOption", null);
        var response2 = await _httpClients[httpClientIndex].PostAsync("v6/TestingIdempotentAPIPerMethod/testUseIdempotencyOption", null);

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
    [InlineData(WebApiClientIndex)]
    [InlineData(WebMinimalApiClientIndex)]
    public async Task PostTestObject_ShouldReturnCachedResponse(int httpClientIndex)
    {
        // Arrange
        var guid = Guid.NewGuid().ToString();

        _httpClients[httpClientIndex].DefaultRequestHeaders.Clear();
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
    [InlineData(WebApiClientIndex)]
    [InlineData(WebMinimalApiClientIndex)]
    public async Task PostTestTheSameRequestObject_WithDifferentIdempotencyKeys_ShouldReturnBadRequestResponse(int httpClientIndex)
    {
        // Arrange
        var guid = Guid.NewGuid().ToString();

        _httpClients[httpClientIndex].DefaultRequestHeaders.Clear();
        _httpClients[httpClientIndex].DefaultRequestHeaders.Add("IdempotencyKey", guid);

        var requestDTO1 = new RequestDTOs() { Description = "A request body." };
        var requestDTO2 = new RequestDTOs() { Description = "A different request body with the same IdempotencyKey." };

        // Act
        var response1 = await _httpClients[httpClientIndex].PostAsJsonAsync("v6/TestingIdempotentAPI/testobjectbody", requestDTO1);
        var response2 = await _httpClients[httpClientIndex].PostAsJsonAsync("v6/TestingIdempotentAPI/testobjectbody", requestDTO2);

        // Assert
        response1.StatusCode.Should().Be(HttpStatusCode.OK);
        response2.StatusCode.Should().Be(HttpStatusCode.BadRequest, "Because the IdempotencyKey exists for a different request body.");

        var content2 = await response2.Content.ReadAsStringAsync();
        content2.Should().MatchRegex("The Idempotency header key value '.*' was used in a different request\\.");
    }

    [Theory]
    [InlineData(WebApiClientIndex)]
    [InlineData(WebMinimalApiClientIndex)]
    public async Task PostTestObjectWithHttpError_ShouldReturnExpectedStatusCode_NotCaching(int httpClientIndex)
    {
        // Arrange
        var guid = Guid.NewGuid().ToString();

        _httpClients[httpClientIndex].DefaultRequestHeaders.Clear();
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
    [InlineData(WebApiClientIndex)]
    [InlineData(WebMinimalApiClientIndex)]
    public async Task PostTestObjectWithHttpError_ShouldReturnExpectedStatusCode_Cached(int httpClientIndex)
    {
        // Arrange
        var guid = Guid.NewGuid().ToString();

        _httpClients[httpClientIndex].DefaultRequestHeaders.Clear();
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
    [InlineData(WebApiClientIndex)]
    [InlineData(WebMinimalApiClientIndex)]
    public async Task PostRequestsConcurrent_OnSameAPI_WithErrorResponse_ShouldReturnTheErrorAndA409Response(int httpClientIndex)
    {
        // Arrange
        var guid = Guid.NewGuid().ToString();

        _httpClients[httpClientIndex].DefaultRequestHeaders.Clear();
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
    [InlineData(WebApiClientIndex)]
    [InlineData(WebMinimalApiClientIndex)]
    public async Task Post_DifferentEndpoints_SameIdempotentKey_ShouldReturnFailure(int httpClientIndex)
    {
        // Arrange
        var guid = Guid.NewGuid().ToString();

        _httpClients[httpClientIndex].DefaultRequestHeaders.Clear();
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

    [Theory]
    [InlineData(WebApiClientIndex)]
    //[InlineData(WebMinimalApiClientIndex)]
    public async Task PostTest_WhenIdempotencyIsOptional_ShouldReturnResponse(int httpClientIndex)
    {
        // Arrange
        _httpClients[httpClientIndex].DefaultRequestHeaders.Clear();

        // Act
        var response1 = await _httpClients[httpClientIndex].PostAsync("v6/TestingIdempotentOptionalAPI/test", null);
        var response2 = await _httpClients[httpClientIndex].PostAsync("v6/TestingIdempotentOptionalAPI/test", null);

        // Assert
        var content1 = await response1.Content.ReadAsStringAsync();
        var content2 = await response2.Content.ReadAsStringAsync();
        _testOutputHelper.WriteLine($"content1: {Environment.NewLine}{content1}");
        _testOutputHelper.WriteLine($"content2: {Environment.NewLine}{content2}");

        response1.StatusCode.Should().Be(HttpStatusCode.OK, content1);
        response2.StatusCode.Should().Be(HttpStatusCode.OK, content2);

        content1.Should().NotBeNull();
        content1.Should().NotBe("null");

        content2.Should().NotBeNull();
        content2.Should().NotBe("null");

        content1.Should().NotBe(content2);
    }

    [Theory]
    [InlineData(WebApiClientIndex)]
    //[InlineData(WebMinimalApiClientIndex)]
    public async Task PostTestObject_WhenIdempotencyIsOptional__ShouldReturnResponse(int httpClientIndex)
    {
        // Arrange
        _httpClients[httpClientIndex].DefaultRequestHeaders.Clear();

        // Act
        var response1 = await _httpClients[httpClientIndex].PostAsync("v6/TestingIdempotentOptionalAPI/testobject", null);
        var response2 = await _httpClients[httpClientIndex].PostAsync("v6/TestingIdempotentOptionalAPI/testobject", null);

        // Assert
        var content1 = await response1.Content.ReadAsStringAsync();
        var content2 = await response2.Content.ReadAsStringAsync();
        _testOutputHelper.WriteLine($"content1: {Environment.NewLine}{content1}");
        _testOutputHelper.WriteLine($"content2: {Environment.NewLine}{content2}");

        response1.StatusCode.Should().Be(HttpStatusCode.OK, content1);
        response2.StatusCode.Should().Be(HttpStatusCode.OK, content2);

        content1.Should().NotBeNull();
        content1.Should().NotBe("null");

        content2.Should().NotBeNull();
        content2.Should().NotBe("null");

        content1.Should().NotBe(content2);
    }
}