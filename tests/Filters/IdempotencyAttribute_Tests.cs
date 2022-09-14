using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using FluentAssertions;
using IdempotentAPI.Cache;
using IdempotentAPI.Filters;
using IdempotentAPI.Helpers;
using IdempotentAPI.Tests.Helpers;
using IdempotentAPI.xUnit.ApplicationServices.DTOs;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Internal;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Primitives;
using Moq;
using Xunit;

namespace IdempotentAPI.xUnit.Filters
{
    public class IdempotencyAttribute_Tests : IClassFixture<MemoryDistributedCacheFixture>
    {
        private readonly string _headerKeyName;
        private readonly string _distributedCacheKeysPrefix;
        private readonly ILoggerFactory _loggerFactory;

        // DistributedCache that will be used from different sequential tests.
        private readonly MemoryDistributedCacheFixture _sharedDistributedCache;

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
            return ArrangeActionContextMock(httpMethod, new HeaderDictionary(), string.Empty, new HeaderDictionary(), null, null);
        }
        private ActionContext ArrangeActionContextMock(string httpMethod, HeaderDictionary requestHeaders)
        {
            return ArrangeActionContextMock(httpMethod, requestHeaders, string.Empty, new HeaderDictionary(), null, null);
        }
        private ActionContext ArrangeActionContextMock(string httpMethod, HeaderDictionary requestHeaders, object requestBody, HeaderDictionary responseHeaders, IActionResult actionResult, int? statusCode)
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
                httpResponse.SetupGet(c => c.StatusCode).Returns(statusCode ?? 500);
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
            var ex = Assert.Throws<ArgumentNullException>(() => idempotencyAttributeFilter.OnActionExecuting(actionExecutingContext));

            // Assert (Exception message)
            Assert.Equal($"Value cannot be null. (Parameter 'An {nameof(IIdempotencyCache)} is not configured. You should register the required services by using the \"AddIdempotentAPIUsing{{YourCacheProvider}}\" function.')", ex.Message);
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
        [InlineData("POST", CacheImplementationEnum.DistributedCache)]
        [InlineData("PATCH", CacheImplementationEnum.DistributedCache)]
        [InlineData("POST", CacheImplementationEnum.FusionCache)]
        [InlineData("PATCH", CacheImplementationEnum.FusionCache)]
        public void ThrowsException_IfIdempotencyKeyHeaderNotExistsOnPostAndPatch(string httpMethod, CacheImplementationEnum cacheImplementation)
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

            IIdempotencyCache distributedCache = MemoryDistributedCacheFixture.CreateCacheInstance(cacheImplementation);
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
        [InlineData("POST", CacheImplementationEnum.DistributedCache)]
        [InlineData("PATCH", CacheImplementationEnum.DistributedCache)]
        [InlineData("POST", CacheImplementationEnum.FusionCache)]
        [InlineData("PATCH", CacheImplementationEnum.FusionCache)]
        public void ThrowsException_IfIdempotencyKeyHeaderExistsWithoutValueOnPostAndPatch(string httpMethod, CacheImplementationEnum cacheImplementation)
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


            IIdempotencyCache distributedCache = MemoryDistributedCacheFixture.CreateCacheInstance(cacheImplementation);
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
        [InlineData("POST", CacheImplementationEnum.DistributedCache)]
        [InlineData("PATCH", CacheImplementationEnum.DistributedCache)]
        [InlineData("POST", CacheImplementationEnum.FusionCache)]
        [InlineData("PATCH", CacheImplementationEnum.FusionCache)]
        public void ThrowsException_IfMultipleIdempotencyKeyHeaderExistsOnPostAndPatch(string httpMethod, CacheImplementationEnum cacheImplementation)
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

            IIdempotencyCache distributedCache = MemoryDistributedCacheFixture.CreateCacheInstance(cacheImplementation);
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
        [InlineData("GET", CacheImplementationEnum.DistributedCache)]
        [InlineData("CONNECT", CacheImplementationEnum.DistributedCache)]
        [InlineData("DELETE", CacheImplementationEnum.DistributedCache)]
        [InlineData("HEAD", CacheImplementationEnum.DistributedCache)]
        [InlineData("OPTIONS", CacheImplementationEnum.DistributedCache)]
        [InlineData("PUT", CacheImplementationEnum.DistributedCache)]
        [InlineData("TRACE", CacheImplementationEnum.DistributedCache)]
        [InlineData("GET", CacheImplementationEnum.FusionCache)]
        [InlineData("CONNECT", CacheImplementationEnum.FusionCache)]
        [InlineData("DELETE", CacheImplementationEnum.FusionCache)]
        [InlineData("HEAD", CacheImplementationEnum.FusionCache)]
        [InlineData("OPTIONS", CacheImplementationEnum.FusionCache)]
        [InlineData("PUT", CacheImplementationEnum.FusionCache)]
        [InlineData("TRACE", CacheImplementationEnum.FusionCache)]
        public void SetsContextResultToNull_IfHttpRequestMethodIsNotPostOrPatch(string httpMethod, CacheImplementationEnum cacheImplementation)
        {
            // Arrange
            var actionContext = ArrangeActionContextMock(httpMethod);
            var actionExecutingContext = new ActionExecutingContext(
                actionContext,
                new List<IFilterMetadata>(),
                new Dictionary<string, object>(),
                Mock.Of<Controller>()
            );

            IIdempotencyCache distributedCache = MemoryDistributedCacheFixture.CreateCacheInstance(cacheImplementation);
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
        [InlineData("POST", "b8fcc234-e1bd-11e9-81b4-2a2ae2dbcce4", CacheImplementationEnum.DistributedCache)]
        [InlineData("PATCH", "c45ca868-fa74-11e9-8f0b-362b9e155667", CacheImplementationEnum.DistributedCache)]
        [InlineData("POST", "b8fcc234-e1bd-11e9-81b4-2a2ae2dbcce4", CacheImplementationEnum.FusionCache)]
        [InlineData("PATCH", "c45ca868-fa74-11e9-8f0b-362b9e155667", CacheImplementationEnum.FusionCache)]
        public void SetInDistributionCache_IfValidRequestNotCached(string httpMethod, string idempotencyKey, CacheImplementationEnum cacheImplementation)
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

            var actionContext = ArrangeActionContextMock(httpMethod, requestHeaders, requestBodyString, responseHeaders, controllerExecutionResult, controllerExecutionResult.StatusCode);
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

            IIdempotencyCache idempotencyCache = MemoryDistributedCacheFixture.CreateCacheInstance(cacheImplementation);

            var idempotencyAttributeFilter = new IdempotencyAttributeFilter(idempotencyCache, _loggerFactory, true, 1, _headerKeyName, _distributedCacheKeysPrefix);


            // Act Part 1 (check cache):
            idempotencyAttributeFilter.OnActionExecuting(actionExecutingContext);

            // Assert Part 1:
            Assert.Null(actionExecutingContext.Result);

            // Act Part 2:
            idempotencyAttributeFilter.OnResultExecuted(resultExecutedContext);

            // Assert Part 2:
            byte[] cachedDataBytes = idempotencyCache.GetOrDefault(
                distributedCacheKey,
                defaultValue: null);

            IReadOnlyDictionary<string, object> cachedData = cachedDataBytes.DeSerialize<IReadOnlyDictionary<string, object>>();

            Assert.NotNull(cachedData);
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
        [InlineData("POST", "d8fcc234-e1bd-11e9-81b4-2a2ae2dbcca1", CacheImplementationEnum.DistributedCache)]
        [InlineData("PATCH", "e45ca868-fa74-11e9-8f0b-362b9e1556a2", CacheImplementationEnum.DistributedCache)]
        [InlineData("POST", "d8fcc234-e1bd-11e9-81b4-2a2ae2dbcca1", CacheImplementationEnum.FusionCache)]
        [InlineData("PATCH", "e45ca868-fa74-11e9-8f0b-362b9e1556a2", CacheImplementationEnum.FusionCache)]
        public void SetInDistributionCache_WithObjectResult_IfValidRequestNotCached(string httpMethod, string idempotencyKey, CacheImplementationEnum cacheImplementation)
        {
            // Arrange

            // Prepare the body and headers for the Request and Response:
            string distributedCacheKey = _distributedCacheKeysPrefix + idempotencyKey;
            string requestBodyString = @"{""message"":""This is a dummy message""}";
            var requestHeaders = new HeaderDictionary
            {
                { "Content-Type", "application/json" },
                { _headerKeyName, idempotencyKey }
            };

            bool cacheOnlySuccessResponses = true;
            int statusCode = StatusCodes.Status200OK;

            var controllerExecutionResult = new ObjectResult(
                new ResponseModelBasic()
                {
                    Id = 1,
                    CreatedOn = new DateTime(2019, 10, 12, 5, 25, 25)
                })
            {
                StatusCode = statusCode,
            };

            var responseHeaders = new HeaderDictionary
            {
                new KeyValuePair<string, StringValues>("CustomHeader01", new StringValues("CustomHeaderValue01")),
                new KeyValuePair<string, StringValues>("CustomHeader02", new StringValues("CustomHeaderValue02"))
            };

            var actionContext = ArrangeActionContextMock(
                httpMethod,
                requestHeaders,
                requestBodyString,
                responseHeaders,
                controllerExecutionResult,
                statusCode);

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

            IIdempotencyCache idempotencyCache = MemoryDistributedCacheFixture.CreateCacheInstance(cacheImplementation);

            var idempotencyAttributeFilter = new IdempotencyAttributeFilter(idempotencyCache, _loggerFactory, true, 1, _headerKeyName, _distributedCacheKeysPrefix, cacheOnlySuccessResponses);


            // Act Part 1 (check cache):
            idempotencyAttributeFilter.OnActionExecuting(actionExecutingContext);

            // Assert Part 1:
            Assert.Null(actionExecutingContext.Result);

            // Act Part 2:
            idempotencyAttributeFilter.OnResultExecuted(resultExecutedContext);

            // Assert Part 2:
            byte[] cachedDataBytes = idempotencyCache.GetOrDefault(
                distributedCacheKey,
                defaultValue: null);

            IReadOnlyDictionary<string, object> cachedData = cachedDataBytes.DeSerialize<IReadOnlyDictionary<string, object>>();

            Assert.NotNull(cachedData);
        }


        /// <summary>
        /// Scenario:
        /// The Idempotency Attribute is enabled, receiving a POST or PATCH request with an IdempotencyKey Header
        /// which doesn't exist in the DistributionCache. We test consecutively *failed* requests by setting the
        /// `CacheOnlySuccessResponses` to `False`. In such a case, we are caching the response of the first request.
        ///
        /// Action:
        /// Part 1 & 2 : Perform a request to cache a 406-NotAcceptable Object result (CacheOnlySuccessResponses == False)
        /// Part 3: Perform a consecutive request with the same IdempotencyKey.
        ///
        /// Expectation:
        /// - The response of the first request should be cached.
        /// - The result of the consecutive requests should have the same response data and HTTP status code as the
        ///   response of the first request.
        /// </summary>
        [Theory]
        [InlineData("POST", "d8fcc234-e1bd-11e9-81b4-2a2ae2dbcca1", CacheImplementationEnum.DistributedCache)]
        [InlineData("PATCH", "e45ca868-fa74-11e9-8f0b-362b9e1556a2", CacheImplementationEnum.DistributedCache)]
        [InlineData("POST", "d8fcc234-e1bd-11e9-81b4-2a2ae2dbcca1", CacheImplementationEnum.FusionCache)]
        [InlineData("PATCH", "e45ca868-fa74-11e9-8f0b-362b9e1556a2", CacheImplementationEnum.FusionCache)]
        public void SetInDistributionCache_WithObjectResultAndDisabledCacheOnlySuccessResponses_IfValidRequestNotCached(string httpMethod, string idempotencyKey, CacheImplementationEnum cacheImplementation)
        {
            // Arrange

            // Prepare the body and headers for the Request and Response:
            string distributedCacheKey = _distributedCacheKeysPrefix + idempotencyKey;
            string requestBodyString = @"{""message"":""This is a dummy message""}";
            var requestHeaders = new HeaderDictionary
            {
                { "Content-Type", "application/json" },
                { _headerKeyName, idempotencyKey }
            };

            bool cacheOnlySuccessResponses = false;
            HttpStatusCode httpStatusCode = HttpStatusCode.NotAcceptable;
            HttpStatusCode expectedResponseStatusCode = HttpStatusCode.NotAcceptable;
            int statusCode = (int)httpStatusCode;

            var controllerExecutionResult = new ObjectResult(new ResponseModelBasic() { Id = 1, CreatedOn = new DateTime(2019, 10, 12, 5, 25, 25) })
            {
                StatusCode = statusCode,
            };

            var responseHeaders = new HeaderDictionary();

            var actionContext = ArrangeActionContextMock(
                httpMethod,
                requestHeaders,
                requestBodyString,
                responseHeaders,
                controllerExecutionResult,
                statusCode);

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

            IIdempotencyCache idempotencyCache = MemoryDistributedCacheFixture.CreateCacheInstance(cacheImplementation);

            var idempotencyRequest1 = new IdempotencyAttributeFilter(
                idempotencyCache,
                _loggerFactory,
                true,
                1,
                _headerKeyName,
                _distributedCacheKeysPrefix,
                cacheOnlySuccessResponses);

            var idempotencyRequest2 = new IdempotencyAttributeFilter(
                idempotencyCache,
                _loggerFactory,
                true,
                1,
                _headerKeyName,
                _distributedCacheKeysPrefix,
                cacheOnlySuccessResponses);

            // Act Part 1 (check cache):
            idempotencyRequest1.OnActionExecuting(actionExecutingContext);

            // Assert Part 1:
            Assert.Null(actionExecutingContext.Result);

            // Act Part 2:
            idempotencyRequest1.OnResultExecuted(resultExecutedContext);

            // Assert Part 2:
            byte[] cachedDataBytes = idempotencyCache.GetOrDefault(
                distributedCacheKey,
                defaultValue: null);

            IReadOnlyDictionary<string, object> cachedData = cachedDataBytes.DeSerialize<IReadOnlyDictionary<string, object>>();
            Assert.NotNull(cachedData);

            // Act Part 3: Re-Execute the Request and check the response.
            idempotencyRequest2.OnActionExecuting(actionExecutingContext);

            Assert.NotNull(actionExecutingContext.Result);

            Assert.IsType<ObjectResult>(actionExecutingContext.Result);
            actionExecutingContext.Result.Should().BeEquivalentTo(controllerExecutionResult);

            Assert.Equal((int)expectedResponseStatusCode, ((ObjectResult)actionExecutingContext.Result).StatusCode);
        }


        /// <summary>
        /// Scenario:
        /// The Idempotency Attribute is enabled, receiving a POST or PATCH request with an IdempotencyKey Header
        /// which doesn't exist in the DistributionCache. We test consecutively *failed* requests by setting the
        /// `CacheOnlySuccessResponses` to `True`. In such a case, we are NOT caching the response of the first request.
        ///
        /// Action:
        /// Part 1 & 2 : Perform a request to cache a 406-NotAcceptable Object result (CacheOnlySuccessResponses == True)
        /// Part 3: Perform a consecutive *success* request with the same IdempotencyKey.
        ///
        /// Expectation:
        /// - The response of the first request should NOT be cached.
        /// - The result of the consecutive requests can be different.
        /// </summary>
        [Theory]
        [InlineData("POST", "d8fcc234-e1bd-11e9-81b4-2a2ae2dbcca1", CacheImplementationEnum.DistributedCache)]
        [InlineData("PATCH", "e45ca868-fa74-11e9-8f0b-362b9e1556a2", CacheImplementationEnum.DistributedCache)]
        [InlineData("POST", "d8fcc234-e1bd-11e9-81b4-2a2ae2dbcca1", CacheImplementationEnum.FusionCache)]
        [InlineData("PATCH", "e45ca868-fa74-11e9-8f0b-362b9e1556a2", CacheImplementationEnum.FusionCache)]
        public void SetInDistributionCache_WithObjectResultAndEnabledCacheOnlySuccessResponses_IfValidRequestNotCached(string httpMethod, string idempotencyKey, CacheImplementationEnum cacheImplementation)
        {
            // Arrange

            // Prepare the body and headers for the Request and Response:
            string distributedCacheKey = _distributedCacheKeysPrefix + idempotencyKey;
            string requestBodyString = @"{""message"":""This is a dummy message""}";
            var requestHeaders = new HeaderDictionary
            {
                { "Content-Type", "application/json" },
                { _headerKeyName, idempotencyKey }
            };

            bool cacheOnlySuccessResponses = true;
            HttpStatusCode httpStatusCode = HttpStatusCode.NotAcceptable;
            int statusCode = (int)httpStatusCode;

            var controllerExecutionResult = new ObjectResult(new ResponseModelBasic() { Id = 1, CreatedOn = new DateTime(2019, 10, 12, 5, 25, 25) })
            {
                StatusCode = statusCode,
            };

            var responseHeaders = new HeaderDictionary();

            var actionContext = ArrangeActionContextMock(
                httpMethod,
                requestHeaders,
                requestBodyString,
                responseHeaders,
                controllerExecutionResult,
                statusCode);

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

            IIdempotencyCache idempotencyCache = MemoryDistributedCacheFixture.CreateCacheInstance(cacheImplementation);

            var idempotencyRequest1 = new IdempotencyAttributeFilter(
                idempotencyCache,
                _loggerFactory,
                true,
                1,
                _headerKeyName,
                _distributedCacheKeysPrefix,
                cacheOnlySuccessResponses);

            var idempotencyRequest2 = new IdempotencyAttributeFilter(
                idempotencyCache,
                _loggerFactory,
                true,
                1,
                _headerKeyName,
                _distributedCacheKeysPrefix,
                cacheOnlySuccessResponses);

            // Act Part 1 (check cache):
            idempotencyRequest1.OnActionExecuting(actionExecutingContext);

            // Assert Part 1:
            Assert.Null(actionExecutingContext.Result);

            // Act Part 2:
            idempotencyRequest1.OnResultExecuted(resultExecutedContext);

            // Assert Part 2:
            byte[] cachedDataBytes = idempotencyCache.GetOrDefault(
                distributedCacheKey,
                defaultValue: null);

            IReadOnlyDictionary<string, object> cachedData = cachedDataBytes.DeSerialize<IReadOnlyDictionary<string, object>>();
            Assert.Null(cachedData);

            // Act Part 3: Re-Execute the Request and check the response.
            idempotencyRequest2.OnActionExecuting(actionExecutingContext);
            Assert.Null(actionExecutingContext.Result);
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
        [Theory]
        [InlineData(CacheImplementationEnum.DistributedCache)]
        [InlineData(CacheImplementationEnum.FusionCache)]
        public void SetInDistributionCache_IfValidFormFileRequestNotCached(CacheImplementationEnum cacheImplementation)
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

            var actionContext = ArrangeActionContextMock(httpMethod, requestHeaders, requestBodyFile, responseHeaders, controllerExecutionResult, controllerExecutionResult.StatusCode);
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


            IIdempotencyCache distributedCache = MemoryDistributedCacheFixture.CreateCacheInstance(cacheImplementation);
            var idempotencyAttributeFilter = new IdempotencyAttributeFilter(distributedCache, _loggerFactory, true, 1, _headerKeyName, _distributedCacheKeysPrefix);


            // Act Part 1 (check cache):
            idempotencyAttributeFilter.OnActionExecuting(actionExecutingContext);

            // Assert Part 1:
            Assert.Null(actionExecutingContext.Result);

            // Act Part 2:
            idempotencyAttributeFilter.OnResultExecuted(resultExecutedContext);

            // Assert Part 2:
            byte[] cachedDataBytes = distributedCache.GetOrDefault(
                distributedCacheKey,
                defaultValue: null);

            IReadOnlyDictionary<string, object> cachedData = cachedDataBytes.DeSerialize<IReadOnlyDictionary<string, object>>();

            Assert.NotNull(cachedData);
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
        [InlineData("POST", "e514ce65-e324-4c42-8fcd-575aa86ae1f3", CacheImplementationEnum.DistributedCache, ObjectResultEnum.OkObjectResult)]
        [InlineData("PATCH", "9099b60c-000d-4e83-9f9e-f342f6ad0f04", CacheImplementationEnum.DistributedCache, ObjectResultEnum.OkObjectResult)]
        [InlineData("POST", "e514ce65-e324-4c42-8fcd-575aa86ae1f3", CacheImplementationEnum.FusionCache, ObjectResultEnum.OkObjectResult)]
        [InlineData("PATCH", "9099b60c-000d-4e83-9f9e-f342f6ad0f04", CacheImplementationEnum.FusionCache, ObjectResultEnum.OkObjectResult)]

        [InlineData("POST", "513b1efc-ffa5-42d9-ab25-95f9b1f8da87", CacheImplementationEnum.DistributedCache, ObjectResultEnum.ObjectResult)]
        [InlineData("PATCH", "b0aa2a96-a905-4e3b-9f36-8529427f15b4", CacheImplementationEnum.DistributedCache, ObjectResultEnum.ObjectResult)]
        [InlineData("POST", "513b1efc-ffa5-42d9-ab25-95f9b1f8da87", CacheImplementationEnum.FusionCache, ObjectResultEnum.ObjectResult)]
        [InlineData("PATCH", "b0aa2a96-a905-4e3b-9f36-8529427f15b4", CacheImplementationEnum.FusionCache, ObjectResultEnum.ObjectResult)]
        public void Step1_SetInDistributionCache_IfValidRequestNotCached_WithInflight(string httpMethod, string idempotencyKey, CacheImplementationEnum cacheImplementation, ObjectResultEnum resultType)
        {
            // Arrange

            // Prepare the body and headers for the Request and Response:
            string distributedCacheKey = _distributedCacheKeysPrefix + idempotencyKey;
            string requestBodyString = @"{""message"":""This is a dummy message""}";
            var requestHeaders = new HeaderDictionary();
            requestHeaders.Add("Content-Type", "application/json");
            requestHeaders.Add(_headerKeyName, idempotencyKey);

            // Execution Result
            var expectedCachedModel = new ResponseModelBasic() { Id = 1, CreatedOn = new DateTime(2019, 10, 12, 5, 25, 25) };

            ObjectResult controllerExecutionResult;
            Type expectedObjectResultType;
            int expectedStatusCode = StatusCodes.Status200OK;
            switch (resultType)
            {
                case ObjectResultEnum.OkObjectResult:
                    controllerExecutionResult = new OkObjectResult(expectedCachedModel);
                    expectedObjectResultType = typeof(OkObjectResult);
                    break;
                default:
                    controllerExecutionResult = new ObjectResult(expectedCachedModel)
                    {
                        StatusCode = expectedStatusCode,
                    };
                    expectedObjectResultType = typeof(ObjectResult);
                    break;
            }


            var responseHeaders = new HeaderDictionary();
            responseHeaders.Add(new KeyValuePair<string, StringValues>("CustomHeader01", new StringValues("CustomHeaderValue01")));
            responseHeaders.Add(new KeyValuePair<string, StringValues>("CustomHeader02", new StringValues("CustomHeaderValue02")));

            var actionContext = ArrangeActionContextMock(httpMethod, requestHeaders, requestBodyString, responseHeaders, controllerExecutionResult, expectedStatusCode);
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

            IIdempotencyCache idempotencyCache = _sharedDistributedCache.GetIdempotencyCache(cacheImplementation);

            var idempotencyAttributeFilter = new IdempotencyAttributeFilter(idempotencyCache, _loggerFactory, true, 1, _headerKeyName, _distributedCacheKeysPrefix);

            var idempotencyAttributeFilterRequest2 = new IdempotencyAttributeFilter(idempotencyCache, _loggerFactory, true, 1, _headerKeyName, _distributedCacheKeysPrefix);

            // Act Part 1 (check cache):
            idempotencyAttributeFilter.OnActionExecuting(actionExecutingContext);

            // Assert Part 1:
            Assert.Null(actionExecutingContext.Result);

            // Act Part 2 - Since we haven't called OnResultExecuted the result of the first request
            // should still be inflight and we should have a 409 Conflict result.
            idempotencyAttributeFilterRequest2.OnActionExecuting(inflightExecutingContext);
            Assert.NotNull(inflightExecutingContext.Result);
            Assert.Equal(typeof(ConflictResult), inflightExecutingContext.Result.GetType());
            Assert.Equal(409, ((ConflictResult)inflightExecutingContext.Result).StatusCode);

            // Act Part 3:
            idempotencyAttributeFilter.OnResultExecuted(resultExecutedContext);

            // Assert Part 3:
            byte[] cachedDataBytes = idempotencyCache.GetOrDefault(
                distributedCacheKey,
                defaultValue: null);

            IReadOnlyDictionary<string, object> cachedData = cachedDataBytes.DeSerialize<IReadOnlyDictionary<string, object>>();

            Assert.NotNull(cachedData);

            // Act 4 rerun the request that failed cause the first was in flight
            idempotencyAttributeFilterRequest2.OnActionExecuting(inflightExecutingContext);

            //Assert : Part 4
            // The result of the above should be coming from the cache so we should have a result
            Assert.Equal(expectedObjectResultType, inflightExecutingContext.Result.GetType());
            Assert.Equal(typeof(ResponseModelBasic), ((ObjectResult)inflightExecutingContext.Result).Value.GetType());
            Assert.Equal(expectedStatusCode, inflightExecutingContext.HttpContext.Response.StatusCode);


            var responseValue = ((ObjectResult)inflightExecutingContext.Result).Value;
            responseValue.Should().BeEquivalentTo(expectedCachedModel);
        }


        /// <summary>
        /// Scenario:
        /// The Idempotency Attribute is enabled, receiving a POST or PATCH request with an IdempotencyKey Header,
        /// which exists in the DistributionCache (from previous request. see "SetInDistributionCache_IfValidRequestNotCached_WithInflight"),
        /// with two response headers.
        ///
        /// Action:
        /// Return the cached response (with the two response headers)
        /// </summary>
        [Theory]
        [InlineData("POST", "e514ce65-e324-4c42-8fcd-575aa86ae1f3", CacheImplementationEnum.DistributedCache, ObjectResultEnum.OkObjectResult)]
        [InlineData("PATCH", "9099b60c-000d-4e83-9f9e-f342f6ad0f04", CacheImplementationEnum.DistributedCache, ObjectResultEnum.OkObjectResult)]
        [InlineData("POST", "e514ce65-e324-4c42-8fcd-575aa86ae1f3", CacheImplementationEnum.FusionCache, ObjectResultEnum.OkObjectResult)]
        [InlineData("PATCH", "9099b60c-000d-4e83-9f9e-f342f6ad0f04", CacheImplementationEnum.FusionCache, ObjectResultEnum.OkObjectResult)]

        [InlineData("POST", "513b1efc-ffa5-42d9-ab25-95f9b1f8da87", CacheImplementationEnum.DistributedCache, ObjectResultEnum.ObjectResult)]
        [InlineData("PATCH", "b0aa2a96-a905-4e3b-9f36-8529427f15b4", CacheImplementationEnum.DistributedCache, ObjectResultEnum.ObjectResult)]
        [InlineData("POST", "513b1efc-ffa5-42d9-ab25-95f9b1f8da87", CacheImplementationEnum.FusionCache, ObjectResultEnum.ObjectResult)]
        [InlineData("PATCH", "b0aa2a96-a905-4e3b-9f36-8529427f15b4", CacheImplementationEnum.FusionCache, ObjectResultEnum.ObjectResult)]
        public void Step2_SetResultFromDistributionCache_IfValidRequestIsCached(
            string httpMethod,
            string idempotencyKey,
            CacheImplementationEnum cacheImplementation,
            ObjectResultEnum resultType)
        {
            // Arrange
            string requestBodyString = @"{""message"":""This is a dummy message""}";
            int expectedResponseHeadersCount = 2;
            var requestHeaders = new HeaderDictionary();
            requestHeaders.Add("Content-Type", "application/json");
            requestHeaders.Add(_headerKeyName, idempotencyKey);

            // The expected result
            int statusCode = StatusCodes.Status200OK;
            var expectedCachedModel = new ResponseModelBasic() { Id = 1, CreatedOn = new DateTime(2019, 10, 12, 5, 25, 25) };

            ObjectResult expectedExecutionResult;
            switch (resultType)
            {
                case ObjectResultEnum.OkObjectResult:
                    expectedExecutionResult = new OkObjectResult(expectedCachedModel);
                    break;
                default:
                    expectedExecutionResult = new ObjectResult(expectedCachedModel)
                    {
                        StatusCode = statusCode,
                    };
                    break;
            }


            var actionContext = ArrangeActionContextMock(
                httpMethod,
                requestHeaders,
                requestBodyString,
                new HeaderDictionary(),
                null,
                statusCode);

            var actionExecutingContext = new ActionExecutingContext(
                actionContext,
                new List<IFilterMetadata>(),
                new Dictionary<string, object>(),
                Mock.Of<Controller>()
            );

            IIdempotencyCache idempotencyCache = _sharedDistributedCache.GetIdempotencyCache(cacheImplementation);

            var idempotencyAttributeFilter = new IdempotencyAttributeFilter(idempotencyCache, _loggerFactory, true, 1, _headerKeyName, _distributedCacheKeysPrefix);


            // Act
            idempotencyAttributeFilter.OnActionExecuting(actionExecutingContext);


            // Assert (result should not be null)
            actionExecutingContext.Result.Should().NotBeNull();

            // Assert (result should be equal to expected value)
            actionExecutingContext.Result.Should().BeEquivalentTo(expectedExecutionResult);

            // Assert (response headers count)
            actionExecutingContext.HttpContext.Response.Headers.Count.Should().Be(expectedResponseHeadersCount);
        }

        /// <summary>
        /// Scenario:
        /// The Idempotency Attribute is enabled, receiving concurrent POST or PATCH requests
        /// with the same IdempotencyKey Header, which doesn't exist in the DistributionCache.
        /// In such a case, a race condition occurs on getting and setting the value at the distributed cache.
        ///
        /// Expected Behavior:
        /// The controller action should be executed only once (in the first request),
        /// and the subsequent requests should return a 409 Conflict HTTP error code.
        /// </summary>
        /// <param name="httpMethod"></param>
        /// <param name="idempotencyKey"></param>
        [Theory]
        [InlineData("POST", "77f29e49-35b1-464b-bb89-7bae6b013640", CacheImplementationEnum.DistributedCache)]
        [InlineData("PATCH", "973d55be-9365-48ac-b56a-9284f9b43c31", CacheImplementationEnum.DistributedCache)]
        [InlineData("POST", "77f29e49-35b1-464b-bb89-7bae6b013640", CacheImplementationEnum.FusionCache)]
        [InlineData("PATCH", "973d55be-9365-48ac-b56a-9284f9b43c31", CacheImplementationEnum.FusionCache)]
        public void SetInDistributionCache_IfValidConcurrentRequestsNotCached_WithInflight(string httpMethod, string idempotencyKey, CacheImplementationEnum cacheImplementation)
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

            var actionContext = ArrangeActionContextMock(httpMethod, requestHeaders, requestBodyString, responseHeaders, controllerExecutionResult, controllerExecutionResult.StatusCode);
            var firstRequestExecutingContext = new ActionExecutingContext(
                actionContext,
                new List<IFilterMetadata>(),
                new Dictionary<string, object>(),
                Mock.Of<Controller>()
            );

            var secondRequestExecutingContext = new ActionExecutingContext(
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

            IIdempotencyCache idempotencyCache = MemoryDistributedCacheFixture.CreateCacheInstance(cacheImplementation);

            var idempotencyAttributeFilter = new IdempotencyAttributeFilter(idempotencyCache, _loggerFactory, true, 1, _headerKeyName, _distributedCacheKeysPrefix);
            var idempotencyAttributeFilterRequest2 = new IdempotencyAttributeFilter(idempotencyCache, _loggerFactory, true, 1, _headerKeyName, _distributedCacheKeysPrefix);

            // Act with concurrent requests (check cache):
            var firstRequestTask = Task.Run(() => idempotencyAttributeFilter.OnActionExecuting(firstRequestExecutingContext));
            var secondRequestTask = Task.Run(() => idempotencyAttributeFilterRequest2.OnActionExecuting(secondRequestExecutingContext));

            Task.WaitAll(firstRequestTask, secondRequestTask);

            // One of the requests should pass, and the other should return a 409 Conflict result.
            IActionResult passedActionResult;
            IActionResult conflictedActionResult;

            if (firstRequestExecutingContext.Result is null)
            {
                passedActionResult = firstRequestExecutingContext.Result;
                conflictedActionResult = secondRequestExecutingContext.Result;
            }
            else
            {
                passedActionResult = secondRequestExecutingContext.Result;
                conflictedActionResult = firstRequestExecutingContext.Result;
            }

            // Assert Part 1 (passed request):
            Assert.Null(passedActionResult);

            // Act Part 2 (409 Conflict result.): Since we haven't called OnResultExecuted the
            // result of the first request should still be inflight and we should have a 409
            // Conflict result.
            Assert.NotNull(conflictedActionResult);
            Assert.Equal(typeof(ConflictResult), conflictedActionResult.GetType());
            Assert.Equal(409, ((ConflictResult)conflictedActionResult).StatusCode);
        }

        /// <summary>
        /// Scenario (Issue #37):
        /// The Idempotency Attribute is enabled, receiving a POST or PATCH request with an IdempotencyKey Header,
        /// which doesn't exist in the DistributionCache.
        /// - On a failure execution (i.e, HTTP Error (5xx, 4xx, etc.), Exceptions (e.g. Timeout, etc.), we remove the in-flight flag data.
        /// - Thus, a subsequent request should be accepted.
        /// </summary>
        [Theory]
        [InlineData("POST", "86a402f8-b025-4ac2-8528-9cdd95ebee2e", CacheImplementationEnum.DistributedCache)]
        [InlineData("POST", "86a402f8-b025-4ac2-8528-9cdd95ebee2e", CacheImplementationEnum.FusionCache)]
        [InlineData("PATCH", "49db5069-1433-44c9-a76e-27e18911d0fb", CacheImplementationEnum.DistributedCache)]
        [InlineData("PATCH", "49db5069-1433-44c9-a76e-27e18911d0fb", CacheImplementationEnum.FusionCache)]
        public void OnFailedExecutions_TheInflightShouldBeCleared_ToAcceptSubsequentRequest(string httpMethod, string idempotencyKey, CacheImplementationEnum cacheImplementation)
        {
            // Arrange

            // Prepare the body and headers for the Request and Response:
            string distributedCacheKey = _distributedCacheKeysPrefix + idempotencyKey;
            string requestBodyString = @"{""message"":""This is a dummy message""}";
            var requestHeaders = new HeaderDictionary
            {
                { "Content-Type", "application/json" },
                { _headerKeyName, idempotencyKey }
            };

            // Execution Result
            var expectedCachedModel = new ResponseModelBasic() { Id = 1, CreatedOn = new DateTime(2019, 10, 12, 5, 25, 25) };
            ObjectResult controllerExecutionResult = new OkObjectResult(expectedCachedModel);
            var responseHeaders = new HeaderDictionary();

            var actionContext = ArrangeActionContextMock(httpMethod, requestHeaders, requestBodyString, responseHeaders, controllerExecutionResult, null);
            var inflightExecutingContext = new ActionExecutingContext(
                actionContext,
                new List<IFilterMetadata>(),
                new Dictionary<string, object>(),
                Mock.Of<Controller>()
            );

            IIdempotencyCache idempotencyCache = MemoryDistributedCacheFixture.CreateCacheInstance(cacheImplementation);
            var idempotencyAttributeFilter = new IdempotencyAttributeFilter(idempotencyCache, _loggerFactory, true, 1, _headerKeyName, _distributedCacheKeysPrefix);


            // Act & Assert Part 1: Before the execution of the controller we store in-flight data (cache exists)
            idempotencyAttributeFilter.OnActionExecuting(inflightExecutingContext);
            byte[] cachedDataBytes = idempotencyCache.GetOrDefault(distributedCacheKey, defaultValue: null);
            Assert.NotNull(cachedDataBytes);
            Assert.Null(inflightExecutingContext.Result);

            // Act & Assert Part 2: On a failure execution, we remove the in-flight flag data.
            idempotencyAttributeFilter.OnActionExecuted(new ActionExecutedContext(actionContext, new List<IFilterMetadata>(), Mock.Of<Controller>())
            {
                Exception = new Exception("A dummy exception"),
            });

            cachedDataBytes = idempotencyCache.GetOrDefault(distributedCacheKey, defaultValue: null);
            Assert.Null(cachedDataBytes);
        }

        /// <summary>
        /// Scenario (Issue #37):
        /// The Idempotency Attribute is enabled, receiving a POST or PATCH request with an IdempotencyKey Header,
        /// which doesn't exist in the DistributionCache. The following scenarios should be asserted
        ///  - When the response is an error, but an exception is NOT thrown, the cached data continue to exist.
        ///  - When cacheOnlySuccessResponses is Enabled, we DO NOT cache the response.
        /// </summary>
        [Theory]
        [InlineData("POST", "86a402f8-b025-4ac2-8528-9cdd95ebee2e", CacheImplementationEnum.DistributedCache, true)]
        [InlineData("POST", "86a402f8-b025-4ac2-8528-9cdd95ebee2e", CacheImplementationEnum.FusionCache, true)]
        [InlineData("PATCH", "49db5069-1433-44c9-a76e-27e18911d0fb", CacheImplementationEnum.DistributedCache, true)]
        [InlineData("PATCH", "49db5069-1433-44c9-a76e-27e18911d0fb", CacheImplementationEnum.FusionCache, true)]

        [InlineData("POST", "86a402f8-b025-4ac2-8528-9cdd95ebee2e", CacheImplementationEnum.DistributedCache, false)]
        [InlineData("POST", "86a402f8-b025-4ac2-8528-9cdd95ebee2e", CacheImplementationEnum.FusionCache, false)]
        [InlineData("PATCH", "49db5069-1433-44c9-a76e-27e18911d0fb", CacheImplementationEnum.DistributedCache, false)]
        [InlineData("PATCH", "49db5069-1433-44c9-a76e-27e18911d0fb", CacheImplementationEnum.FusionCache, false)]
        public void OnFailedExecutions_ClearCachedResponseBasedOnTheTheCacheOnlySuccessResponsesConfig_ToAcceptSubsequentRequest(
            string httpMethod,
            string idempotencyKey,
            CacheImplementationEnum cacheImplementation,
            bool cacheOnlySuccessResponses)
        {
            // Arrange

            // Prepare the body and headers for the Request and Response:
            string distributedCacheKey = _distributedCacheKeysPrefix + idempotencyKey;
            string requestBodyString = @"{""message"":""This is a dummy message""}";
            var requestHeaders = new HeaderDictionary
            {
                { "Content-Type", "application/json" },
                { _headerKeyName, idempotencyKey }
            };

            // Execution Result
            var expectedCachedModel = new ResponseModelBasic() { Id = 1, CreatedOn = new DateTime(2019, 10, 12, 5, 25, 25) };

            ObjectResult controllerExecutionResult = new OkObjectResult(expectedCachedModel);

            int expectedStatusCode = StatusCodes.Status500InternalServerError;
            var responseHeaders = new HeaderDictionary();

            var actionContextWithError = ArrangeActionContextMock(httpMethod, requestHeaders, requestBodyString, responseHeaders, controllerExecutionResult, expectedStatusCode);

            var inflightExecutingContextWithError = new ActionExecutingContext(
                actionContextWithError,
                new List<IFilterMetadata>(),
                new Dictionary<string, object>(),
                Mock.Of<Controller>()
            );

            var resultExecutedContextWithError = new ResultExecutedContext(
                actionContextWithError,
                new List<IFilterMetadata>(),
                controllerExecutionResult,
                Mock.Of<Controller>());

            IIdempotencyCache idempotencyCache = MemoryDistributedCacheFixture.CreateCacheInstance(cacheImplementation);

            var idempotencyAttributeFilter = new IdempotencyAttributeFilter(idempotencyCache, _loggerFactory, true, 1, _headerKeyName, _distributedCacheKeysPrefix, cacheOnlySuccessResponses);


            // Act & Assert Part 1: Before the execution of the controller we store in-flight data (cache exists)
            idempotencyAttributeFilter.OnActionExecuting(inflightExecutingContextWithError);
            byte[] cachedDataBytes = idempotencyCache.GetOrDefault(distributedCacheKey, defaultValue: null);
            Assert.NotNull(cachedDataBytes);
            Assert.Null(inflightExecutingContextWithError.Result);


            // Act & Assert Part 2: When the response is an error, but an exception is NOT thrown, the cached data continue to exist.
            idempotencyAttributeFilter.OnActionExecuted(new ActionExecutedContext(inflightExecutingContextWithError, new List<IFilterMetadata>(), Mock.Of<Controller>()));
            cachedDataBytes = idempotencyCache.GetOrDefault(distributedCacheKey, defaultValue: null);
            Assert.NotNull(cachedDataBytes);
            Assert.Null(inflightExecutingContextWithError.Result);


            // Assert Part 3: When cacheOnlySuccessResponses is Enabled, we DO NOT cache the response.
            idempotencyAttributeFilter.OnResultExecuted(resultExecutedContextWithError);
            cachedDataBytes = idempotencyCache.GetOrDefault(distributedCacheKey, defaultValue: null);


            if (cacheOnlySuccessResponses)
            {
                Assert.Null(cachedDataBytes);
                Assert.Null(inflightExecutingContextWithError.Result);
            }
            else
            {
                Assert.NotNull(cachedDataBytes);
                IReadOnlyDictionary<string, object> cacheData = cachedDataBytes.DeSerialize<IReadOnlyDictionary<string, object>>();
                Assert.NotNull(cacheData);
                cacheData.Should().NotContainKey("Request.Inflight");
            }
        }

    }
}
