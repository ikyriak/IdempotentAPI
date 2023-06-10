using System.Net;
using FluentAssertions;
using Xunit;

namespace IdempotentAPI.IntegrationTests
{
    // Semi-Automated Tests.
    // TODO: Fully automated tests.
    //
    // Current Semi-Automated Requirements:
    // - Local Redis in Port 6379.
    public class TestWebAPIsConcurrentTests :
        IClassFixture<WebApi1ApplicationFactory>,
        IClassFixture<WebApi2ApplicationFactory>,
        IClassFixture<WebMinimalApi1ApplicationFactory>,
        IClassFixture<WebMinimalApi2ApplicationFactory>
    {
        private readonly HttpClient[] _httpClientsInstance1;
        private readonly HttpClient[] _httpClientsInstance2;

        public TestWebAPIsConcurrentTests(
            WebApi1ApplicationFactory api1ApplicationFactory,
            WebApi2ApplicationFactory api2ApplicationFactory,
            WebMinimalApi1ApplicationFactory minimalApi1ApplicationFactory,
            WebMinimalApi2ApplicationFactory minimalApi2ApplicationFactory
            )
        {
            _httpClientsInstance1 = new[]{
                api1ApplicationFactory.CreateClient(),
                minimalApi1ApplicationFactory.CreateClient()
            };

            _httpClientsInstance2 = new[]{
                api2ApplicationFactory.CreateClient(),
                minimalApi2ApplicationFactory.CreateClient()
            };

        }

        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        public async Task PostRequestsConcurrent_OnClusterEnvironment_WithErrorResponse_ShouldReturnTheErrorAndA409Response(int httpClientIndex)
        {
            // Arrange
            var guid = Guid.NewGuid().ToString();

            _httpClientsInstance1[httpClientIndex].DefaultRequestHeaders.Add("IdempotencyKey", guid);
            _httpClientsInstance2[httpClientIndex].DefaultRequestHeaders.Add("IdempotencyKey", guid);

            // Act
            var httpGetTask1 = _httpClientsInstance1[httpClientIndex].PostAsync("v6/TestingIdempotentAPI/customNotAcceptable406?delaySeconds=5", null);
            var httpGetTask2 = _httpClientsInstance2[httpClientIndex].PostAsync("v6/TestingIdempotentAPI/customNotAcceptable406?delaySeconds=5", null);

            await Task.WhenAll(httpGetTask1, httpGetTask2);

            // Assert
            var resultStatusCodes = new List<HttpStatusCode>() { httpGetTask1.Result.StatusCode, httpGetTask2.Result.StatusCode };
            resultStatusCodes.Should().Contain(HttpStatusCode.NotAcceptable);
            resultStatusCodes.Should().Contain(HttpStatusCode.Conflict);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        public async Task PostRequestsConsecutively_WithErrorResponse_ShouldReturnErrorResponsesWithDifferentData(int httpClientIndex)
        {
            // Arrange
            var guid = Guid.NewGuid().ToString();

            _httpClientsInstance1[httpClientIndex].DefaultRequestHeaders.Add("IdempotencyKey", guid);
            _httpClientsInstance2[httpClientIndex].DefaultRequestHeaders.Add("IdempotencyKey", guid);

            // Act
            var httpResponse1 = await _httpClientsInstance1[httpClientIndex].PostAsync("v6/TestingIdempotentAPI/customNotAcceptable406?delaySeconds=5", null);
            var httpResponse2 = await _httpClientsInstance2[httpClientIndex].PostAsync("v6/TestingIdempotentAPI/customNotAcceptable406?delaySeconds=5", null);


            // Assert
            httpResponse1.StatusCode.Should().Be(HttpStatusCode.NotAcceptable);
            httpResponse2.StatusCode.Should().Be(HttpStatusCode.NotAcceptable);

            var responseContent1 = await httpResponse1.Content.ReadAsStringAsync();
            var responseContent2 = await httpResponse2.Content.ReadAsStringAsync();

            responseContent1.Should().NotBe(responseContent2);
        }
    }
}