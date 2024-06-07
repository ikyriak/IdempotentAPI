using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using IdempotentAPI.TestWebAPIs.DTOs;
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
        IClassFixture<WebMinimalApi2ApplicationFactory>,
        IClassFixture<WebTestFastEndpointsAPI1ApplicationFactory>,
        IClassFixture<WebTestFastEndpointsAPI2ApplicationFactory>
    {
        private readonly HttpClient[] _httpClientsInstance1;
        private readonly HttpClient[] _httpClientsInstance2;

        private const int WebApiClientIndex = 0;
        private const int WebMinimalApiClientIndex = 1;
        private const int WebFastEndpointsAPIClientIndex = 2;

        public TestWebAPIsConcurrentTests(
            WebApi1ApplicationFactory api1ApplicationFactory,
            WebApi2ApplicationFactory api2ApplicationFactory,
            WebMinimalApi1ApplicationFactory minimalApi1ApplicationFactory,
            WebMinimalApi2ApplicationFactory minimalApi2ApplicationFactory,
            WebTestFastEndpointsAPI1ApplicationFactory fastEndpointsAPI1ApplicationFactory,
            WebTestFastEndpointsAPI2ApplicationFactory fastEndpointsAPI2ApplicationFactory
            )
        {
            _httpClientsInstance1 = new[]{
                // WebApiClientIndex
                api1ApplicationFactory.CreateClient(),
                // WebMinimalApiClientIndex
                minimalApi1ApplicationFactory.CreateClient(),
                // WebFastEndpointsAPIClientIndex
                fastEndpointsAPI1ApplicationFactory.CreateClient()
            };

            _httpClientsInstance2 = new[]{
                // WebApiClientIndex
                api2ApplicationFactory.CreateClient(),
                 // WebMinimalApiClientIndex
                minimalApi2ApplicationFactory.CreateClient(),
                // WebFastEndpointsAPIClientIndex
                fastEndpointsAPI2ApplicationFactory.CreateClient()
            };
        }

        [Theory]
        [InlineData(WebApiClientIndex)]
        [InlineData(WebMinimalApiClientIndex)]
        public async Task PostRequestsConcurrent_OnClusterEnvironment_WithErrorResponse_ShouldReturnTheErrorAndA409Response(int httpClientIndex)
        {
            // Arrange
            var guid = Guid.NewGuid().ToString();

            _httpClientsInstance1[httpClientIndex].DefaultRequestHeaders.Clear();
            _httpClientsInstance2[httpClientIndex].DefaultRequestHeaders.Clear();

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
        [InlineData(WebApiClientIndex)]
        [InlineData(WebMinimalApiClientIndex)]
        [InlineData(WebFastEndpointsAPIClientIndex)]
        public async Task PostJsonRequestsConcurrent_OnClusterEnvironment_WithErrorResponse_ShouldReturnTheErrorAndA409Response(int httpClientIndex)
        {
            // Arrange
            var guid = Guid.NewGuid().ToString();

            _httpClientsInstance1[httpClientIndex].DefaultRequestHeaders.Clear();
            _httpClientsInstance2[httpClientIndex].DefaultRequestHeaders.Clear();

            _httpClientsInstance1[httpClientIndex].DefaultRequestHeaders.Add("IdempotencyKey", guid);
            _httpClientsInstance2[httpClientIndex].DefaultRequestHeaders.Add("IdempotencyKey", guid);

            var dummyRequest = new RequestDTOs() { Description = "Empty Body!" };

            // Act
            var httpGetTask1 = _httpClientsInstance1[httpClientIndex].PostAsJsonAsync("v6/TestingIdempotentAPI/customNotAcceptable406?delaySeconds=5", dummyRequest);
            var httpGetTask2 = _httpClientsInstance2[httpClientIndex].PostAsJsonAsync("v6/TestingIdempotentAPI/customNotAcceptable406?delaySeconds=5", dummyRequest);

            await Task.WhenAll(httpGetTask1, httpGetTask2);

            // Assert
            var resultStatusCodes = new List<HttpStatusCode>() { httpGetTask1.Result.StatusCode, httpGetTask2.Result.StatusCode };
            resultStatusCodes.Should().Contain(HttpStatusCode.NotAcceptable);
            resultStatusCodes.Should().Contain(HttpStatusCode.Conflict);
        }

        [Theory]
        [InlineData(WebApiClientIndex)]
        [InlineData(WebMinimalApiClientIndex)]
        public async Task PostRequestsConsecutively_WithErrorResponse_ShouldReturnErrorResponsesWithDifferentData(int httpClientIndex)
        {
            // Arrange
            var guid = Guid.NewGuid().ToString();

            _httpClientsInstance1[httpClientIndex].DefaultRequestHeaders.Clear();
            _httpClientsInstance2[httpClientIndex].DefaultRequestHeaders.Clear();

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

        [Theory]
        [InlineData(WebApiClientIndex)]
        [InlineData(WebMinimalApiClientIndex)]
        [InlineData(WebFastEndpointsAPIClientIndex)]
        public async Task PostJsonRequestsConsecutively_WithErrorResponse_ShouldReturnErrorResponsesWithDifferentData(int httpClientIndex)
        {
            // Arrange
            var guid = Guid.NewGuid().ToString();

            _httpClientsInstance1[httpClientIndex].DefaultRequestHeaders.Clear();
            _httpClientsInstance2[httpClientIndex].DefaultRequestHeaders.Clear();

            _httpClientsInstance1[httpClientIndex].DefaultRequestHeaders.Add("IdempotencyKey", guid);
            _httpClientsInstance2[httpClientIndex].DefaultRequestHeaders.Add("IdempotencyKey", guid);

            var dummyRequest = new RequestDTOs() { Description = "Empty Body!" };

            // Act
            var httpResponse1 = await _httpClientsInstance1[httpClientIndex].PostAsJsonAsync("v6/TestingIdempotentAPI/customNotAcceptable406?delaySeconds=5", dummyRequest);
            var httpResponse2 = await _httpClientsInstance2[httpClientIndex].PostAsJsonAsync("v6/TestingIdempotentAPI/customNotAcceptable406?delaySeconds=5", dummyRequest);


            // Assert
            httpResponse1.StatusCode.Should().Be(HttpStatusCode.NotAcceptable);
            httpResponse2.StatusCode.Should().Be(HttpStatusCode.NotAcceptable);

            var responseContent1 = await httpResponse1.Content.ReadAsStringAsync();
            var responseContent2 = await httpResponse2.Content.ReadAsStringAsync();

            responseContent1.Should().NotBe(responseContent2);
        }
    }
}