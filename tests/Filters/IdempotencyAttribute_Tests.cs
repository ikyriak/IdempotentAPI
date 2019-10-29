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


namespace IdempotentAPI.xUnit.Filters
{
    public class IdempotencyAttribute_Tests
    {
        private readonly string _headerKeyName;
        private readonly string _distributedCacheKeysPrefix;

        public IdempotencyAttribute_Tests()
        {
            _headerKeyName = "IdempotencyKey";
            _distributedCacheKeysPrefix = "IdempAPI_";
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

            switch (requestBody)
            {
                case string requestBodyString:
                    if (!string.IsNullOrEmpty(requestBodyString))
                    {
                        request.SetupGet(r => r.Path).Returns("/resource");
                        request.SetupGet(r => r.QueryString).Returns(new QueryString());
                        request.SetupGet(c => c.ContentLength).Returns(requestBodyString.Length);
                        request.SetupGet(r => r.Body).Returns(new MemoryStream(Encoding.UTF8.GetBytes(requestBodyString)));
                        request.SetupGet(r => r.ContentType).Returns(@"json object");
                    }
                    break;
                // Mock Request's File:
                case FormFile requestBodyFile:
                    if (requestBodyFile != null)
                    {
                        request.SetupGet(r => r.Path).Returns("/resource");
                        request.SetupGet(r => r.QueryString).Returns(new QueryString());
                        request.SetupGet(c => c.ContentLength).Returns(requestBodyFile.Length);
                        //request.SetupGet(r => r.Body).Returns(new MemoryStream(requestBodyFile.Serialize()));
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
                httpResponse.SetupGet(r => r.ContentType).Returns(@"json object");
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

            var idempotencyAttributeFilter = new IdempotencyAttributeFilter(null, false, 1, _headerKeyName,_distributedCacheKeysPrefix);

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

            var idempotencyAttributeFilter = new IdempotencyAttributeFilter(null, true, 1, _headerKeyName, _distributedCacheKeysPrefix);

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

            var distributedCache = new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions()));
            var idempotencyAttributeFilter = new IdempotencyAttributeFilter(distributedCache, true, 1, _headerKeyName, _distributedCacheKeysPrefix);

            // Act
            var ex = Assert.Throws<ArgumentNullException>(() => idempotencyAttributeFilter.OnActionExecuting(actionExecutingContext));

            // Assert (Exception message)
            Assert.Equal("The Idempotency header key is not found.\r\nParameter name: IdempotencyKey", ex.Message);
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

            var distributedCache = new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions()));
            var idempotencyAttributeFilter = new IdempotencyAttributeFilter(distributedCache, true, 1, _headerKeyName, _distributedCacheKeysPrefix);

            // Act
            var ex = Assert.Throws<ArgumentNullException>(() => idempotencyAttributeFilter.OnActionExecuting(actionExecutingContext));

            // Assert (Exception message)
            Assert.Equal("An Idempotency header value is not found.\r\nParameter name: IdempotencyKey", ex.Message);
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

            var distributedCache = new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions()));
            var idempotencyAttributeFilter = new IdempotencyAttributeFilter(distributedCache, true, 1, _headerKeyName, _distributedCacheKeysPrefix);

            // Act
            var ex = Assert.Throws<ArgumentException>(() => idempotencyAttributeFilter.OnActionExecuting(actionExecutingContext));

            // Assert (Exception message)
            Assert.Equal("Multiple Idempotency keys were found.\r\nParameter name: IdempotencyKey", ex.Message);
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
            var idempotencyAttributeFilter = new IdempotencyAttributeFilter(distributedCache, true, 1, _headerKeyName, _distributedCacheKeysPrefix);

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
        [InlineData("POST")]
        [InlineData("PATCH")]
        public void SetInDistributionCache_IfValidRequestNotCached(string httpMethod)
        {
            // Arrange

            // Prepare the body and headers for the Request and Response:
            const string IDEMPOTENCY_KEY = "b8fcc234-e1bd-11e9-81b4-2a2ae2dbcce4";
            string distributedCacheKey = _distributedCacheKeysPrefix + IDEMPOTENCY_KEY;
            string requestBodyString = @"{""message"":""This is a dummy message""}";
            var requestHeaders = new HeaderDictionary();
            requestHeaders.Add(_headerKeyName, IDEMPOTENCY_KEY);
                        
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


            var distributedCache = new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions()));
            var idempotencyAttributeFilter = new IdempotencyAttributeFilter(distributedCache, true, 1, _headerKeyName, _distributedCacheKeysPrefix);


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
            var idempotencyAttributeFilter = new IdempotencyAttributeFilter(distributedCache, true, 1, _headerKeyName, _distributedCacheKeysPrefix);


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
        /// The Idempotency Attribute is enabled, receiving a POST request with the IdempotencyKey Header "b8fcc234-e1bd-11e9-81b4-2a2ae2dbcce4",
        /// which exists in the DistributionCache (from previous request) with two response headers
        /// 
        /// Action:
        /// Return the cached response (with the two response headers)
        /// </summary>
        [Fact]
        public void SetResultFromDistributionCache_IfValidRequestIsCached()
        {
            // Arrange
            const string IDEMPOTENCY_KEY = "b8fcc234-e1bd-11e9-81b4-2a2ae2dbcce4";
            string distributedCacheKey = _distributedCacheKeysPrefix + IDEMPOTENCY_KEY;
            string httpMethod = "POST";
            string requestBodyString = @"{""message"":""This is a dummy message""}";
            int expectedResponseHeadersCount = 2;
            var requestHeaders = new HeaderDictionary();
            requestHeaders.Add(_headerKeyName, IDEMPOTENCY_KEY);
            
            // The expected result
            var expectedExecutionResult = new OkObjectResult(new ResponseModelBasic() { Id = 1, CreatedOn = new DateTime(2019, 10, 12, 5, 25, 25) });

            var actionContext = ArrangeActionContextMock(httpMethod, requestHeaders, requestBodyString, new HeaderDictionary(), null);
            var actionExecutingContext = new ActionExecutingContext(
                actionContext,
                new List<IFilterMetadata>(),
                new Dictionary<string, object>(),
                Mock.Of<Controller>()
            );

            // Mock an existing cached POST request:
            byte[] cachedResponse = new byte[] { 31, 139, 8, 0, 0, 0, 0, 0, 0, 11, 237, 87, 223, 107, 212, 88, 20, 158, 116, 218, 52, 217, 86, 221, 82, 89, 177, 44, 50, 162, 40, 186, 99, 204, 252, 232, 164, 1, 187, 90, 167, 174, 214, 181, 206, 236, 78, 245, 165, 148, 241, 230, 230, 204, 54, 78, 38, 25, 239, 189, 41, 206, 190, 232, 171, 175, 130, 255, 147, 143, 62, 20, 21, 4, 193, 101, 89, 4, 65, 4, 97, 97, 65, 88, 239, 73, 38, 90, 181, 91, 124, 216, 98, 119, 219, 12, 76, 114, 239, 61, 247, 228, 59, 231, 124, 231, 158, 147, 140, 146, 201, 100, 254, 150, 23, 222, 241, 26, 196, 135, 223, 148, 70, 143, 11, 232, 24, 213, 208, 247, 129, 10, 47, 12, 184, 113, 30, 2, 96, 30, 53, 102, 189, 120, 130, 176, 222, 181, 226, 226, 98, 95, 176, 33, 152, 23, 252, 146, 207, 117, 56, 13, 153, 239, 57, 249, 220, 85, 96, 92, 138, 77, 151, 13, 19, 127, 249, 92, 53, 242, 69, 196, 96, 58, 128, 72, 48, 226, 231, 115, 245, 200, 241, 61, 250, 35, 244, 22, 194, 54, 4, 211, 142, 101, 145, 73, 58, 89, 41, 216, 165, 50, 152, 83, 246, 82, 62, 85, 94, 115, 174, 75, 16, 255, 174, 242, 165, 65, 105, 231, 112, 95, 143, 86, 13, 59, 93, 194, 128, 105, 23, 8, 95, 110, 120, 191, 194, 46, 185, 243, 42, 241, 35, 168, 19, 143, 241, 76, 54, 147, 213, 238, 110, 228, 148, 254, 253, 220, 141, 136, 248, 158, 232, 165, 250, 174, 21, 54, 213, 67, 75, 218, 31, 27, 129, 90, 107, 195, 127, 58, 86, 139, 75, 154, 140, 150, 62, 32, 255, 198, 240, 33, 139, 68, 197, 209, 86, 140, 9, 102, 209, 48, 34, 204, 96, 42, 33, 240, 236, 239, 219, 34, 74, 131, 111, 228, 57, 178, 61, 76, 69, 238, 101, 219, 208, 27, 90, 65, 115, 148, 1, 117, 72, 78, 236, 254, 25, 110, 68, 192, 133, 49, 15, 98, 57, 116, 85, 21, 89, 90, 175, 53, 22, 148, 191, 164, 99, 208, 57, 42, 178, 97, 52, 21, 171, 19, 177, 172, 234, 200, 231, 147, 12, 120, 24, 49, 10, 202, 159, 169, 228, 136, 156, 31, 79, 37, 127, 138, 128, 245, 18, 47, 169, 163, 49, 179, 94, 167, 114, 187, 229, 232, 235, 84, 110, 150, 8, 130, 39, 152, 186, 71, 206, 158, 41, 210, 73, 211, 166, 100, 210, 158, 42, 149, 11, 78, 139, 182, 44, 199, 132, 178, 69, 29, 215, 44, 153, 45, 66, 203, 174, 105, 185, 101, 199, 177, 166, 10, 196, 106, 57, 96, 149, 173, 138, 233, 218, 149, 150, 93, 44, 185, 149, 10, 85, 94, 165, 47, 25, 75, 192, 240, 174, 12, 38, 200, 112, 17, 17, 241, 106, 232, 130, 166, 221, 71, 48, 47, 83, 185, 113, 57, 218, 251, 78, 174, 26, 6, 2, 2, 177, 208, 235, 130, 186, 87, 174, 140, 92, 231, 97, 144, 11, 227, 120, 40, 47, 210, 61, 223, 36, 6, 244, 247, 92, 0, 226, 202, 240, 232, 251, 80, 239, 243, 84, 102, 63, 122, 55, 86, 119, 83, 24, 82, 86, 198, 75, 159, 64, 239, 162, 216, 234, 192, 150, 42, 86, 235, 161, 184, 228, 113, 177, 217, 199, 206, 78, 121, 92, 167, 60, 62, 30, 216, 98, 167, 209, 255, 132, 29, 139, 241, 9, 24, 23, 100, 172, 116, 250, 183, 152, 174, 19, 253, 146, 247, 225, 210, 1, 44, 135, 184, 254, 110, 45, 251, 112, 39, 42, 155, 147, 179, 207, 228, 97, 185, 227, 220, 205, 161, 252, 71, 37, 63, 123, 235, 11, 103, 180, 154, 139, 139, 98, 196, 69, 216, 73, 170, 166, 89, 208, 15, 98, 154, 61, 149, 44, 64, 38, 168, 135, 62, 145, 40, 234, 135, 49, 29, 15, 124, 144, 142, 219, 163, 101, 83, 158, 164, 221, 196, 17, 105, 244, 87, 73, 23, 17, 247, 38, 71, 229, 248, 142, 50, 239, 81, 22, 242, 176, 37, 140, 25, 222, 189, 12, 162, 26, 50, 48, 230, 87, 168, 81, 107, 39, 120, 146, 29, 249, 220, 63, 10, 226, 195, 123, 168, 69, 163, 248, 121, 80, 137, 235, 216, 150, 93, 154, 42, 218, 174, 75, 160, 98, 142, 30, 147, 120, 46, 206, 185, 208, 233, 134, 216, 63, 205, 212, 231, 140, 155, 87, 2, 79, 188, 215, 93, 248, 92, 55, 4, 145, 239, 43, 143, 83, 195, 143, 99, 19, 150, 152, 17, 199, 84, 255, 14, 123, 40, 228, 204, 151, 38, 51, 86, 10, 181, 233, 73, 253, 124, 168, 201, 229, 247, 183, 214, 92, 73, 180, 202, 38, 90, 211, 244, 124, 159, 175, 49, 111, 145, 194, 8, 90, 63, 177, 102, 118, 8, 109, 57, 189, 142, 215, 140, 153, 110, 87, 190, 156, 160, 81, 13, 96, 43, 30, 5, 110, 204, 46, 212, 56, 54, 146, 113, 211, 57, 47, 59, 89, 255, 44, 225, 30, 197, 116, 24, 63, 53, 231, 126, 223, 110, 54, 207, 18, 218, 150, 134, 254, 224, 129, 239, 78, 156, 170, 50, 32, 2, 220, 90, 240, 241, 146, 132, 183, 11, 3, 134, 56, 110, 63, 88, 189, 183, 122, 249, 145, 54, 150, 194, 85, 13, 84, 184, 54, 3, 99, 191, 155, 133, 177, 20, 186, 122, 114, 125, 137, 226, 200, 91, 44, 104, 38, 141, 154, 17, 0, 0 };
            var distributedCache = new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions()));
            distributedCache.Set(distributedCacheKey, cachedResponse);
            
            var idempotencyAttributeFilter = new IdempotencyAttributeFilter(distributedCache, true, 1, _headerKeyName, _distributedCacheKeysPrefix);


            // Act
            idempotencyAttributeFilter.OnActionExecuting(actionExecutingContext);


            // Assert (result should not be null)
            Assert.NotNull(actionExecutingContext.Result);

            // Assert (result should be equal to expected value)
            expectedExecutionResult.Should().Equals(actionExecutingContext.Result);

            // Assert (response headers count)
            Assert.Equal(expectedResponseHeadersCount, actionExecutingContext.HttpContext.Response.Headers.Count);
        }


    }
}
