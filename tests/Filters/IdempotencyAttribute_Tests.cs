using IdempotentAPI.Filters;
using IdempotentAPI.xUnit.ApplicationServices.DTOs;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Internal;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Moq;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Text;
using Xunit;
using IdempotentAPI.Helpers;
using FluentAssertions;
using Microsoft.Extensions.Primitives;
using IdempotentAPI.Tests.Helpers;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;

namespace IdempotentAPI.xUnit.Filters
{
    public class IdempotencyAttribute_Tests : IClassFixture<MemoryDistributedCacheFixture>
    {
        private readonly string _headerKeyName;
        private readonly string _distributedCacheKeysPrefix;
        private readonly ILoggerFactory _loggerFactory;

        // DistributedCache that will be used from different sequential tests.
        private MemoryDistributedCacheFixture _sharedDistributedCache;

        public IdempotencyAttribute_Tests(MemoryDistributedCacheFixture sharedDistributedCache)
        {
            _headerKeyName = "IdempotencyKey";
            _distributedCacheKeysPrefix = "IdempAPI_";
            _sharedDistributedCache = sharedDistributedCache;

            // Show the logs for Debug:
            IServiceCollection serviceCollection = new ServiceCollection();
            serviceCollection.AddLogging(builder => builder
                .AddDebug()
            );
            _loggerFactory = serviceCollection.BuildServiceProvider().GetService<ILoggerFactory>();
        }

        private ActionContext ArrangeActionContextMock(string httpMethod)
        {
            return ArrangeActionContextMock(httpMethod, new HeaderDictionary(), string.Empty, new HeaderDictionary(), null);
        }
        private ActionContext ArrangeActionContextMock(string httpMethod, HeaderDictionary requestHeaders)
        {
            return ArrangeActionContextMock(httpMethod, requestHeaders, string.Empty, new HeaderDictionary(), null);
        }
        private ActionContext ArrangeActionContextMock(string httpMethod, HeaderDictionary requestHeaders, object requestBody, HeaderDictionary responseHeaders, IStatusCodeActionResult actionResult)
        {
            // Mock Post Request:
            var request = new Mock<HttpRequest>();
            request.Setup(r => r.Method).Returns(httpMethod);

            // Mock Request's Headers (if any):
            request.Setup(r => r.Headers).Returns(requestHeaders);

            // Set the Content-Type based on the request headers.
            string contentType = "json object";
            if (requestHeaders.ContainsKey("Content-Type"))
            {
                contentType = requestHeaders["Content-Type"];
            }

            switch (requestBody)
            {
                case string requestBodyString:
                    if (!string.IsNullOrEmpty(requestBodyString))
                    {
                        request.SetupGet(r => r.Path).Returns("/resource");
                        request.SetupGet(r => r.QueryString).Returns(new QueryString());
                        request.SetupGet(c => c.ContentLength).Returns(requestBodyString.Length);
                        request.SetupGet(r => r.Body).Returns(new MemoryStream(Encoding.UTF8.GetBytes(requestBodyString)));
                        request.SetupGet(r => r.ContentType).Returns(contentType);

                        // The Form throws an exception when the Content-Type is not supported.
                        request.SetupGet(r => r.HasFormContentType).Returns(false);
                        request.SetupGet(r => r.Form).Throws(new InvalidOperationException($"Incorrect Content-Type: {contentType}"));
                    }
                    break;
                // Mock Request's File:
                case FormFile requestBodyFile:
                    if (requestBodyFile != null)
                    {
                        request.SetupGet(r => r.Path).Returns("/resource");
                        request.SetupGet(r => r.QueryString).Returns(new QueryString());
                        request.SetupGet(c => c.ContentLength).Returns(requestBodyFile.Length);
                        request.Setup(r => r.Form.Files).Returns(new FormFileCollection() { requestBodyFile });
                        request.SetupGet(r => r.ContentType).Returns("multipart/form-data; boundary=----WebKitFormBoundary7MA4YWxkTrZu0gW");
                    }
                    break;
            }



            // Mock Request's File:
            //FormFile txtFile = new FormFile(new MemoryStream(Encoding.UTF8.GetBytes("This is a dummy file")), 0, 0, "Data", "dummy.txt");
            //txtFile.Length
            //request.Setup(r => r.Form.Files).Returns(new FormFileCollection() { txtFile });


            // Mock HttpContext Request:
            var httpContext = new Mock<HttpContext>();
            httpContext.Setup(x => x.Request).Returns(request.Object);


            // Mock HttpContext Response:
            var httpResponse = new Mock<HttpResponse>();
            httpResponse.Setup(r => r.Headers).Returns(responseHeaders);
            if (actionResult != null)
            {
                httpResponse.SetupGet(c => c.StatusCode).Returns(actionResult.StatusCode.HasValue ? actionResult.StatusCode.Value : 500);
                httpResponse.SetupGet(c => c.ContentLength).Returns(actionResult is ObjectResult ? ((ObjectResult)actionResult).Value.Serialize().Length : 0);
                httpResponse.SetupGet(r => r.ContentType).Returns(contentType);
            }
            httpContext.SetupGet(c => c.Response).Returns(() => httpResponse.Object);


            var actionContext = new ActionContext(
                httpContext.Object,
                Mock.Of<RouteData>(),
                Mock.Of<ActionDescriptor>()
            );

            return actionContext;
        }



        /// <summary>
        /// Scenario:
        /// The Idempotency Attribute is not enabled (Test OnActionExecuting).
        /// 
        /// Action:
        /// Don't do anything!
        /// </summary>
        [Fact]
        public void SetsContextResultToNull_IfIdempotencyAttributeIsDisabled()
        {
            // Arrange
            var actionContext = ArrangeActionContextMock(HttpMethods.Post);
            var actionExecutingContext = new ActionExecutingContext(
                actionContext,
                new List<IFilterMetadata>(),
                new Dictionary<string, object>(),
                Mock.Of<Controller>()
            );

            var idempotencyAttributeFilter = new IdempotencyAttributeFilter(null, _loggerFactory, false, 1, _headerKeyName, _distributedCacheKeysPrefix);

            // Act
            idempotencyAttributeFilter.OnActionExecuting(actionExecutingContext);

            // Assert (Exception message)
            Assert.Null(actionExecutingContext.Result);
        }


        /// <summary>
        /// Scenario:
        /// The DistributionCache has not been initialized (it's Null).
        /// 
        /// Action:
        /// Throw exception to avoid unexpected requests execution
        /// </summary>
        [Fact]
        public void ThrowsException_IfDistributionCacheIsNull()
        {
            // Arrange
            var actionContext = ArrangeActionContextMock(HttpMethods.Post);
            var actionExecutingContext = new ActionExecutingContext(
                actionContext,
                new List<IFilterMetadata>(),
                new Dictionary<string, object>(),
                Mock.Of<Controller>()
            );

            var idempotencyAttributeFilter = new IdempotencyAttributeFilter(null, _loggerFactory, true, 1, _headerKeyName, _distributedCacheKeysPrefix);

            // Act
            var ex = Assert.Throws<Exception>(() => idempotencyAttributeFilter.OnActionExecuting(actionExecutingContext));

            // Assert (Exception message)
            Assert.Equal("An IDistributedCache is not configured.", ex.Message);
        }


        /// <summary>
        /// Scenario:
        /// The Idempotency Attribute is enabled, receiving a POST or PATCH request without
        /// defining the IdempotencyKey as a header variable.
        /// 
        /// Action:
        /// Throw an ArgumentNullException
        /// 
        /// Background:
        /// Idempotency is performed only for Post and Patch HTTP requests.
        /// </summary>
        /// <param name="httpMethod"></param>
        [Theory]
        [InlineData("POST")]
        [InlineData("PATCH")]
        public void ThrowsException_IfIdempotencyKeyHeaderNotExistsOnPostAndPatch(string httpMethod)
        {
            // Arrange
            var actionContext = ArrangeActionContextMock(httpMethod);
            var actionExecutingContext = new ActionExecutingContext(
                actionContext,
                new List<IFilterMetadata>(),
                new Dictionary<string, object>(),
                Mock.Of<Controller>()
            );

            // Expected error messages per .NET Target Framework:
            List<string> expectedExceptionMessages = new List<string>();

            // .NET Core 2.0 & 2.1 Exception Message:
            expectedExceptionMessages.Add("The Idempotency header key is not found.\r\nParameter name: IdempotencyKey");

            // .NET Core 3.0  Exception Message:
            expectedExceptionMessages.Add("The Idempotency header key is not found. (Parameter 'IdempotencyKey')");

            var distributedCache = new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions()));
            var idempotencyAttributeFilter = new IdempotencyAttributeFilter(distributedCache, _loggerFactory, true, 1, _headerKeyName, _distributedCacheKeysPrefix);

            // Act
            var ex = Assert.Throws<ArgumentNullException>(() => idempotencyAttributeFilter.OnActionExecuting(actionExecutingContext));

            // Assert (Exception message)
            Assert.Contains(ex.Message, expectedExceptionMessages);
        }


        /// <summary>
        /// Scenario:
        /// The Idempotency Attribute is enabled, receiving a POST or PATCH request containing
        /// the IdempotencyKey as a header variable, but without a value
        /// 
        /// Action:
        /// Throw an ArgumentNullException
        /// 
        /// Background:
        /// Idempotency is performed only for Post and Patch HTTP requests.
        /// </summary>
        /// <param name="httpMethod"></param>
        [Theory]
        [InlineData("POST")]
        [InlineData("PATCH")]
        public void ThrowsException_IfIdempotencyKeyHeaderExistsWithoutValueOnPostAndPatch(string httpMethod)
        {
            // Arrange
            var requestHeaders = new HeaderDictionary();
            requestHeaders.Add(_headerKeyName, string.Empty);
            var actionContext = ArrangeActionContextMock(httpMethod, requestHeaders);
            var actionExecutingContext = new ActionExecutingContext(
                actionContext,
                new List<IFilterMetadata>(),
                new Dictionary<string, object>(),
                Mock.Of<Controller>()
            );

            // Expected error messages per .NET Target Framework:
            List<string> expectedExceptionMessages = new List<string>();

            // .NET Core 2.0 & 2.1 Exception Message:
            expectedExceptionMessages.Add("An Idempotency header value is not found.\r\nParameter name: IdempotencyKey");

            // .NET Core 3.0  Exception Message:
            expectedExceptionMessages.Add("An Idempotency header value is not found. (Parameter 'IdempotencyKey')");


            var distributedCache = new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions()));
            var idempotencyAttributeFilter = new IdempotencyAttributeFilter(distributedCache, _loggerFactory, true, 1, _headerKeyName, _distributedCacheKeysPrefix);

            // Act
            var ex = Assert.Throws<ArgumentNullException>(() => idempotencyAttributeFilter.OnActionExecuting(actionExecutingContext));

            // Assert (Exception message)
            Assert.Contains(ex.Message, expectedExceptionMessages);
        }

        /// <summary>
        /// Scenario:
        /// The Idempotency Attribute is enabled, receiving a POST or PATCH request containing
        /// multiple IdempotencyKey as a header variables
        /// 
        /// Action:
        /// Throw an ArgumentException
        /// 
        /// Background:
        /// Idempotency is performed only for Post and Patch HTTP requests.
        /// </summary>
        /// <param name="httpMethod"></param>
        [Theory]
        [InlineData("POST")]
        [InlineData("PATCH")]
        public void ThrowsException_IfMultipleIdempotencyKeyHeaderExistsOnPostAndPatch(string httpMethod)
        {
            // Arrange
            var requestHeaders = new HeaderDictionary();
            var idenpotencyKeys = new StringValues(new string[] { string.Empty, string.Empty });

            requestHeaders.Add(_headerKeyName, idenpotencyKeys);
            var actionContext = ArrangeActionContextMock(httpMethod, requestHeaders);
            var actionExecutingContext = new ActionExecutingContext(
                actionContext,
                new List<IFilterMetadata>(),
                new Dictionary<string, object>(),
                Mock.Of<Controller>()
            );

            // Expected error messages per .NET Target Framework:
            List<string> expectedExceptionMessages = new List<string>();

            // .NET Core 2.0 & 2.1 Exception Message:
            expectedExceptionMessages.Add("Multiple Idempotency keys were found.\r\nParameter name: IdempotencyKey");

            // .NET Core 3.0  Exception Message:
            expectedExceptionMessages.Add("Multiple Idempotency keys were found. (Parameter 'IdempotencyKey')");

            var distributedCache = new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions()));
            var idempotencyAttributeFilter = new IdempotencyAttributeFilter(distributedCache, _loggerFactory, true, 1, _headerKeyName, _distributedCacheKeysPrefix);

            // Act
            var ex = Assert.Throws<ArgumentException>(() => idempotencyAttributeFilter.OnActionExecuting(actionExecutingContext));

            // Assert (Exception message)
            Assert.Contains(ex.Message, expectedExceptionMessages);
        }



        /// <summary>
        /// Scenario: 
        /// The Idempotency Attribute is enabled, receiving a Non-(POST or PATCH) request
        /// 
        /// Action:
        /// Don't do anything! The ContextResult remains Null.
        /// 
        /// Background: 
        /// Idempotency is performed only for Post and Patch HTTP requests.
        /// </summary>
        /// <param name="httpMethod"></param>
        [Theory]
        [InlineData("GET")]
        [InlineData("CONNECT")]
        [InlineData("DELETE")]
        [InlineData("HEAD")]
        [InlineData("OPTIONS")]
        [InlineData("PUT")]
        [InlineData("TRACE")]
        public void SetsContextResultToNull_IfHttpRequestMethodIsNotPostOrPatch(string httpMethod)
        {
            // Arrange
            var actionContext = ArrangeActionContextMock(httpMethod);
            var actionExecutingContext = new ActionExecutingContext(
                actionContext,
                new List<IFilterMetadata>(),
                new Dictionary<string, object>(),
                Mock.Of<Controller>()
            );

            var distributedCache = new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions()));
            var idempotencyAttributeFilter = new IdempotencyAttributeFilter(distributedCache, _loggerFactory, true, 1, _headerKeyName, _distributedCacheKeysPrefix);

            // Act
            idempotencyAttributeFilter.OnActionExecuting(actionExecutingContext);

            // Assert
            Assert.Null(actionExecutingContext.Result);
        }


        /// <summary>
        /// Scenario: 
        /// The Idempotency Attribute is enabled, receiving a POST or PATCH request with an IdempotencyKey Header,
        /// which doesn't exist in the DistributionCache.
        /// 
        /// Action:
        /// Part1: Check that do not exist in the DistributionCache [OnActionExecuting]
        /// Part2: Save in the DistributionCache [OnActionExecuted]
        /// </summary>
        [Theory]
        [InlineData("POST", "b8fcc234-e1bd-11e9-81b4-2a2ae2dbcce4")]
        [InlineData("PATCH", "c45ca868-fa74-11e9-8f0b-362b9e155667")]
        public void SetInDistributionCache_IfValidRequestNotCached(string httpMethod, string idempotencyKey)
        {
            // Arrange

            // Prepare the body and headers for the Request and Response:
            string distributedCacheKey = _distributedCacheKeysPrefix + idempotencyKey;
            string requestBodyString = @"{""message"":""This is a dummy message""}";
            var requestHeaders = new HeaderDictionary();
            requestHeaders.Add("Content-Type", "application/json");
            requestHeaders.Add(_headerKeyName, idempotencyKey);

            var controllerExecutionResult = new OkObjectResult(new ResponseModelBasic() { Id = 1, CreatedOn = new DateTime(2019, 10, 12, 5, 25, 25) });
            var responseHeaders = new HeaderDictionary();
            responseHeaders.Add(new KeyValuePair<string, StringValues>("CustomHeader01", new StringValues("CustomHeaderValue01")));
            responseHeaders.Add(new KeyValuePair<string, StringValues>("CustomHeader02", new StringValues("CustomHeaderValue02")));

            var actionContext = ArrangeActionContextMock(httpMethod, requestHeaders, requestBodyString, responseHeaders, controllerExecutionResult);
            var actionExecutingContext = new ActionExecutingContext(
                actionContext,
                new List<IFilterMetadata>(),
                new Dictionary<string, object>(),
                Mock.Of<Controller>()
            );

            var resultExecutedContext = new ResultExecutedContext(
                actionContext,
                new List<IFilterMetadata>(),
                controllerExecutionResult,
                Mock.Of<Controller>());


            var idempotencyAttributeFilter = new IdempotencyAttributeFilter(_sharedDistributedCache.Cache, _loggerFactory, true, 1, _headerKeyName, _distributedCacheKeysPrefix);


            // Act Part 1 (check cache):
            idempotencyAttributeFilter.OnActionExecuting(actionExecutingContext);

            // Assert Part 1:
            Assert.Null(actionExecutingContext.Result);

            // Act Part 2:
            idempotencyAttributeFilter.OnResultExecuted(resultExecutedContext);

            // Assert Part 2:
            byte[] cachedData = _sharedDistributedCache.Cache.Get(distributedCacheKey);
            Assert.NotNull(cachedData);
        }


        /// <summary>
        /// Scenario: 
        /// The Idempotency Attribute is enabled, receiving a POST FormFile request with an IdempotencyKey Header,
        /// which doesn't exist in the DistributionCache.
        /// 
        /// Action:
        /// Part1: Check that do not exist in the DistributionCache [OnActionExecuting]
        /// Part2: Save in the DistributionCache [OnActionExecuted]
        /// </summary>
        [Fact]
        public void SetInDistributionCache_IfValidFormFileRequestNotCached()
        {
            // Arrange

            // Prepare the body and headers for the Request and Response:
            const string IDEMPOTENCY_KEY = "b8fcc234-e1bd-11e9-81b4-2a2ae2dbcce4";
            string distributedCacheKey = _distributedCacheKeysPrefix + IDEMPOTENCY_KEY;
            string httpMethod = "POST";
            FormFile requestBodyFile = new FormFile(new MemoryStream(Encoding.UTF8.GetBytes("This is a dummy file")), 0, 0, "Data", "dummy.txt");
            var requestHeaders = new HeaderDictionary();
            requestHeaders.Add(_headerKeyName, IDEMPOTENCY_KEY);

            var controllerExecutionResult = new OkObjectResult(new ResponseModelBasic() { Id = 1, CreatedOn = new DateTime(2019, 10, 12, 5, 25, 25) });
            var responseHeaders = new HeaderDictionary();

            var actionContext = ArrangeActionContextMock(httpMethod, requestHeaders, requestBodyFile, responseHeaders, controllerExecutionResult);
            var actionExecutingContext = new ActionExecutingContext(
                actionContext,
                new List<IFilterMetadata>(),
                new Dictionary<string, object>(),
                Mock.Of<Controller>()
            );

            var resultExecutedContext = new ResultExecutedContext(
                actionContext,
                new List<IFilterMetadata>(),
                controllerExecutionResult,
                Mock.Of<Controller>());


            var distributedCache = new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions()));
            var idempotencyAttributeFilter = new IdempotencyAttributeFilter(distributedCache, _loggerFactory, true, 1, _headerKeyName, _distributedCacheKeysPrefix);


            // Act Part 1 (check cache):
            idempotencyAttributeFilter.OnActionExecuting(actionExecutingContext);

            // Assert Part 1:
            Assert.Null(actionExecutingContext.Result);

            // Act Part 2:
            idempotencyAttributeFilter.OnResultExecuted(resultExecutedContext);

            // Assert Part 2:
            byte[] cachedData = distributedCache.Get(distributedCacheKey);
            Assert.NotNull(cachedData);
        }


        /// <summary>
        /// Scenario: 
        /// The Idempotency Attribute is enabled, receiving a POST or PATCH request with an IdempotencyKey Header,
        /// which exists in the DistributionCache (from previous request. see "SetInDistributionCache_IfValidRequestNotCached"),
        /// with two response headers.
        /// 
        /// Action:
        /// Return the cached response (with the two response headers)
        /// </summary>
        [Theory]
        [InlineData("POST", "b8fcc234-e1bd-11e9-81b4-2a2ae2dbcce4")]
        [InlineData("PATCH", "c45ca868-fa74-11e9-8f0b-362b9e155667")]
        public void SetResultFromDistributionCache_IfValidRequestIsCached(string httpMethod, string idempotencyKey)
        {
            // Arrange
            string requestBodyString = @"{""message"":""This is a dummy message""}";
            int expectedResponseHeadersCount = 2;
            var requestHeaders = new HeaderDictionary();
            requestHeaders.Add("Content-Type", "application/json");
            requestHeaders.Add(_headerKeyName, idempotencyKey);

            // The expected result
            var expectedExecutionResult = new OkObjectResult(new ResponseModelBasic() { Id = 1, CreatedOn = new DateTime(2019, 10, 12, 5, 25, 25) });

            var actionContext = ArrangeActionContextMock(httpMethod, requestHeaders, requestBodyString, new HeaderDictionary(), null);
            var actionExecutingContext = new ActionExecutingContext(
                actionContext,
                new List<IFilterMetadata>(),
                new Dictionary<string, object>(),
                Mock.Of<Controller>()
            );

            var idempotencyAttributeFilter = new IdempotencyAttributeFilter(_sharedDistributedCache.Cache, _loggerFactory, true, 1, _headerKeyName, _distributedCacheKeysPrefix);


            // Act
            idempotencyAttributeFilter.OnActionExecuting(actionExecutingContext);


            // Assert (result should not be null)
            Assert.NotNull(actionExecutingContext.Result);

            // Assert (result should be equal to expected value)
            expectedExecutionResult.Should().Equals(actionExecutingContext.Result);

            // Assert (response headers count)
            Assert.Equal(expectedResponseHeadersCount, actionExecutingContext.HttpContext.Response.Headers.Count);
        }

        /// <summary>
        /// Scenario: 
        /// The Idempotency Attribute is enabled, receiving a POST or PATCH request with an IdempotencyKey Header,
        /// which doesn't exist in the DistributionCache. Then check a second request while the first in still in flight
        /// 
        /// Action:
        /// Part1: Check that do not exist in the DistributionCache [OnActionExecuting]
        /// Part2: Send the same request and check a 409 is returned
        /// Part3: Save in the DistributionCache [OnActionExecuted]
        /// Part4: Resend the same request and ensure the response is the same as part 3
        /// </summary>
        [Theory]
        [InlineData("POST", "3e692c04-c19d-419b-b60c-3cdb6c577686")]
        [InlineData("PATCH", "9099b60c-000d-4e83-9f9e-f342f6ad0f04")]
        public void SetInDistributionCache_IfValidRequestNotCached_WithInflight(string httpMethod, string idempotencyKey)
        {
            // Arrange

            // Prepare the body and headers for the Request and Response:
            string distributedCacheKey = _distributedCacheKeysPrefix + idempotencyKey;
            string requestBodyString = @"{""message"":""This is a dummy message""}";
            var requestHeaders = new HeaderDictionary();
            requestHeaders.Add("Content-Type", "application/json");
            requestHeaders.Add(_headerKeyName, idempotencyKey);

            // Execution Result
            var controllerExecutionResult = new OkObjectResult(new ResponseModelBasic() { Id = 1, CreatedOn = new DateTime(2019, 10, 12, 5, 25, 25) });

            var responseHeaders = new HeaderDictionary();
            responseHeaders.Add(new KeyValuePair<string, StringValues>("CustomHeader01", new StringValues("CustomHeaderValue01")));
            responseHeaders.Add(new KeyValuePair<string, StringValues>("CustomHeader02", new StringValues("CustomHeaderValue02")));

            var actionContext = ArrangeActionContextMock(httpMethod, requestHeaders, requestBodyString, responseHeaders, controllerExecutionResult);
            var actionExecutingContext = new ActionExecutingContext(
                actionContext,
                new List<IFilterMetadata>(),
                new Dictionary<string, object>(),
                Mock.Of<Controller>()
            );

            var inflightExecutingContext = new ActionExecutingContext(
                actionContext,
                new List<IFilterMetadata>(),
                new Dictionary<string, object>(),
                Mock.Of<Controller>()
            );

            var resultExecutedContext = new ResultExecutedContext(
                actionContext,
                new List<IFilterMetadata>(),
                controllerExecutionResult,
                Mock.Of<Controller>());

            var idempotencyAttributeFilter = new IdempotencyAttributeFilter(_sharedDistributedCache.Cache, _loggerFactory, true, 1, _headerKeyName, _distributedCacheKeysPrefix);

            var idempotencyAttributeFilterRequest2 = new IdempotencyAttributeFilter(_sharedDistributedCache.Cache, _loggerFactory, true, 1, _headerKeyName, _distributedCacheKeysPrefix);

            // Act Part 1 (check cache):
            idempotencyAttributeFilter.OnActionExecuting(actionExecutingContext);

            // Assert Part 1:
            Assert.Null(actionExecutingContext.Result);

            // Act Part 2 - since we haven't called OnResultExecuted the result of the first request should still be inflight and we should have a 409 result
            idempotencyAttributeFilterRequest2.OnActionExecuting(inflightExecutingContext);
            Assert.NotNull(inflightExecutingContext.Result);
            Assert.Equal(typeof(ConflictResult), inflightExecutingContext.Result.GetType());
            Assert.Equal(409, ((ConflictResult)inflightExecutingContext.Result).StatusCode);

            // Act Part 3:
            idempotencyAttributeFilter.OnResultExecuted(resultExecutedContext);

            // Assert Part 3:
            byte[] cachedData = _sharedDistributedCache.Cache.Get(distributedCacheKey);
            Assert.NotNull(cachedData);

            // Act 4 rerun the request that failed cause the first was in flight
            idempotencyAttributeFilterRequest2.OnActionExecuting(inflightExecutingContext);

            //Assert : Part 4
            // The result of the above should be coming from the cache so we should have a result
            Assert.Equal(typeof(OkObjectResult), inflightExecutingContext.Result.GetType());
            Assert.Equal(typeof(ResponseModelBasic), ((OkObjectResult)inflightExecutingContext.Result).Value.GetType());                                    
            Assert.Equal(200, ((OkObjectResult)inflightExecutingContext.Result).StatusCode);

        }
    }
}
