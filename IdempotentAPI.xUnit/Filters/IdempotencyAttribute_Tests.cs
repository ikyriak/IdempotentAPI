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


namespace IdempotentAPI.xUnit.Filters
{
    public class IdempotencyAttribute_Tests
    {
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
            if (actionResult != null)
            {
                var httpResponse = new Mock<HttpResponse>();
                httpResponse.SetupGet(c => c.StatusCode).Returns(actionResult.StatusCode.HasValue ? actionResult.StatusCode.Value : 500);
                httpResponse.SetupGet(c => c.ContentLength).Returns(actionResult is ObjectResult ? ((ObjectResult)actionResult).Value.Serialize().Length : 0);
                httpResponse.SetupGet(r => r.ContentType).Returns(@"json object");
                httpResponse.Setup(r => r.Headers).Returns(responseHeaders);
                httpContext.SetupGet(c => c.Response).Returns(() => httpResponse.Object);
            }


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

            var idempotencyAttributeFilter = new IdempotencyAttributeFilter(null, false, 1);

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

            var idempotencyAttributeFilter = new IdempotencyAttributeFilter(null, true, 1);

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
            var idempotencyAttributeFilter = new IdempotencyAttributeFilter(distributedCache, true, 1);

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
            requestHeaders.Add("IdempotencyKey", string.Empty);
            var actionContext = ArrangeActionContextMock(httpMethod, requestHeaders);
            var actionExecutingContext = new ActionExecutingContext(
                actionContext,
                new List<IFilterMetadata>(),
                new Dictionary<string, object>(),
                Mock.Of<Controller>()
            );

            var distributedCache = new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions()));
            var idempotencyAttributeFilter = new IdempotencyAttributeFilter(distributedCache, true, 1);

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
            var idenpotencyKeys = new Microsoft.Extensions.Primitives.StringValues(new string[] { string.Empty, string.Empty });

            requestHeaders.Add("IdempotencyKey", idenpotencyKeys);
            var actionContext = ArrangeActionContextMock(httpMethod, requestHeaders);
            var actionExecutingContext = new ActionExecutingContext(
                actionContext,
                new List<IFilterMetadata>(),
                new Dictionary<string, object>(),
                Mock.Of<Controller>()
            );

            var distributedCache = new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions()));
            var idempotencyAttributeFilter = new IdempotencyAttributeFilter(distributedCache, true, 1);

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
            var idempotencyAttributeFilter = new IdempotencyAttributeFilter(distributedCache, true, 1);

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
            string requestBodyString = @"{""message"":""This is a dummy message""}";
            var requestHeaders = new HeaderDictionary();
            requestHeaders.Add("IdempotencyKey", IDEMPOTENCY_KEY);
                        
            var controllerExecutionResult = new OkObjectResult(new ResponseModelBasic() { Id = 1, CreatedOn = new DateTime(2019, 10, 12, 5, 25, 25) });
            var responseHeaders = new HeaderDictionary();

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
            var idempotencyAttributeFilter = new IdempotencyAttributeFilter(distributedCache, true, 1);


            // Act Part 1 (check cache):
            idempotencyAttributeFilter.OnActionExecuting(actionExecutingContext);

            // Assert Part 1:
            Assert.Null(actionExecutingContext.Result);

            // Act Part 2:
            idempotencyAttributeFilter.OnResultExecuted(resultExecutedContext);

            // Assert Part 2:
            byte[] cachedData = distributedCache.Get(IDEMPOTENCY_KEY);
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
            string httpMethod = "POST";
            FormFile requestBodyFile = new FormFile(new MemoryStream(Encoding.UTF8.GetBytes("This is a dummy file")), 0, 0, "Data", "dummy.txt");
            var requestHeaders = new HeaderDictionary();
            requestHeaders.Add("IdempotencyKey", IDEMPOTENCY_KEY);

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
            var idempotencyAttributeFilter = new IdempotencyAttributeFilter(distributedCache, true, 1);


            // Act Part 1 (check cache):
            idempotencyAttributeFilter.OnActionExecuting(actionExecutingContext);

            // Assert Part 1:
            Assert.Null(actionExecutingContext.Result);

            // Act Part 2:
            idempotencyAttributeFilter.OnResultExecuted(resultExecutedContext);

            // Assert Part 2:
            byte[] cachedData = distributedCache.Get(IDEMPOTENCY_KEY);
            Assert.NotNull(cachedData);
        }


        /// <summary>
        /// Scenario: 
        /// The Idempotency Attribute is enabled, receiving a POST request with the IdempotencyKey Header "b8fcc234-e1bd-11e9-81b4-2a2ae2dbcce4",
        /// which exists in the DistributionCache (from previous request)
        /// 
        /// Action:
        /// Return the cached response
        /// </summary>
        [Fact]
        public void SetResultFromDistributionCache_IfValidRequestIsCached()
        {

            // Arrange
            const string IDEMPOTENCY_KEY = "b8fcc234-e1bd-11e9-81b4-2a2ae2dbcce4";
            string httpMethod = "POST";
            string requestBodyString = @"{""message"":""This is a dummy message""}";
            var requestHeaders = new HeaderDictionary();
            requestHeaders.Add("IdempotencyKey", IDEMPOTENCY_KEY);
            
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
            byte[] cackedResponse = new byte[] { 31, 139, 8, 0, 0, 0, 0, 0, 0, 11, 229, 86, 205, 107, 19, 65, 20, 207, 38, 109, 154, 216, 15, 45, 45, 138, 69, 52, 234, 53, 172, 155, 207, 205, 66, 171, 182, 169, 218, 170, 105, 162, 137, 189, 148, 208, 206, 206, 190, 216, 109, 54, 187, 233, 204, 108, 105, 60, 121, 245, 42, 248, 63, 121, 244, 80, 240, 42, 42, 34, 8, 34, 8, 130, 32, 232, 188, 36, 91, 69, 106, 233, 161, 133, 74, 55, 176, 59, 243, 230, 55, 111, 126, 239, 115, 18, 82, 66, 161, 208, 79, 249, 224, 23, 159, 1, 28, 188, 85, 170, 29, 46, 160, 165, 22, 61, 199, 1, 42, 108, 207, 229, 234, 29, 112, 129, 217, 84, 157, 183, 187, 2, 194, 58, 107, 233, 149, 149, 62, 176, 42, 152, 237, 62, 78, 38, 90, 156, 122, 204, 177, 205, 100, 98, 25, 24, 151, 176, 153, 172, 170, 225, 47, 153, 40, 250, 142, 240, 25, 204, 184, 224, 11, 70, 156, 100, 162, 226, 155, 142, 77, 239, 65, 167, 230, 53, 193, 157, 49, 117, 157, 228, 104, 46, 159, 50, 50, 89, 208, 10, 70, 61, 25, 40, 47, 155, 27, 146, 196, 225, 42, 175, 15, 72, 59, 135, 250, 122, 98, 69, 175, 213, 38, 12, 88, 108, 129, 240, 245, 170, 253, 4, 70, 229, 206, 101, 226, 248, 80, 33, 54, 227, 161, 72, 40, 18, 123, 190, 159, 83, 250, 223, 91, 155, 62, 113, 108, 209, 9, 244, 173, 165, 142, 212, 67, 245, 216, 135, 253, 72, 253, 105, 195, 127, 29, 171, 149, 122, 76, 70, 43, 30, 150, 175, 113, 28, 68, 48, 81, 113, 118, 28, 99, 130, 85, 52, 132, 12, 67, 88, 74, 72, 60, 242, 254, 68, 68, 105, 224, 135, 236, 35, 39, 195, 84, 204, 189, 72, 19, 58, 131, 91, 104, 142, 18, 142, 14, 74, 193, 216, 67, 216, 244, 129, 11, 181, 4, 98, 221, 179, 162, 81, 204, 210, 74, 185, 90, 83, 190, 75, 199, 160, 115, 162, 152, 13, 35, 1, 172, 66, 196, 122, 52, 142, 249, 124, 141, 1, 247, 124, 70, 65, 249, 22, 32, 135, 165, 124, 34, 64, 62, 240, 129, 117, 122, 94, 138, 142, 116, 51, 235, 107, 128, 27, 147, 179, 51, 1, 110, 158, 8, 130, 29, 44, 122, 90, 74, 111, 166, 105, 78, 51, 40, 201, 25, 133, 76, 54, 101, 54, 104, 67, 55, 53, 200, 234, 212, 180, 180, 140, 214, 32, 52, 107, 105, 186, 149, 53, 77, 189, 144, 34, 122, 195, 4, 61, 171, 231, 53, 203, 200, 55, 140, 116, 198, 202, 231, 169, 242, 37, 56, 100, 188, 71, 134, 183, 101, 48, 65, 134, 139, 8, 159, 23, 61, 11, 98, 177, 151, 72, 230, 115, 128, 155, 144, 179, 201, 93, 92, 209, 115, 5, 184, 162, 214, 105, 67, 116, 82, 174, 12, 111, 112, 207, 77, 120, 221, 120, 40, 159, 130, 61, 103, 123, 6, 244, 247, 44, 0, 177, 100, 120, 226, 231, 80, 239, 199, 0, 115, 30, 189, 219, 85, 183, 45, 84, 137, 149, 241, 138, 79, 161, 119, 17, 182, 19, 62, 86, 151, 213, 94, 44, 238, 219, 92, 28, 117, 219, 57, 100, 117, 145, 125, 175, 71, 121, 33, 30, 207, 251, 48, 20, 220, 19, 221, 42, 153, 234, 55, 225, 112, 32, 69, 171, 226, 23, 208, 52, 124, 237, 174, 157, 144, 6, 173, 188, 11, 10, 234, 162, 52, 250, 84, 175, 144, 186, 229, 121, 73, 206, 159, 41, 37, 155, 50, 143, 123, 13, 161, 206, 242, 246, 18, 136, 162, 199, 64, 45, 109, 81, 181, 220, 236, 241, 233, 237, 72, 38, 254, 9, 196, 193, 111, 170, 105, 53, 125, 48, 170, 196, 50, 13, 221, 200, 20, 210, 134, 101, 17, 200, 107, 35, 151, 37, 159, 187, 139, 22, 180, 218, 30, 182, 144, 217, 202, 162, 186, 253, 200, 181, 229, 209, 129, 238, 212, 65, 221, 224, 250, 142, 163, 188, 9, 12, 191, 130, 125, 168, 103, 70, 55, 166, 241, 171, 82, 48, 136, 175, 27, 123, 28, 167, 206, 182, 219, 82, 23, 193, 124, 168, 2, 219, 178, 41, 112, 117, 190, 86, 230, 216, 132, 186, 13, 171, 36, 187, 160, 51, 71, 184, 77, 49, 143, 38, 166, 23, 173, 235, 205, 213, 213, 57, 66, 155, 50, 55, 110, 219, 224, 88, 83, 211, 69, 6, 68, 128, 85, 118, 255, 94, 146, 255, 14, 70, 209, 82, 76, 195, 167, 175, 118, 94, 236, 44, 189, 142, 13, 255, 2, 173, 135, 59, 73, 146, 11, 0, 0 };
            var distributedCache = new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions()));
            distributedCache.Set(IDEMPOTENCY_KEY, cackedResponse);
            
            var idempotencyAttributeFilter = new IdempotencyAttributeFilter(distributedCache, true, 1);


            // Act
            idempotencyAttributeFilter.OnActionExecuting(actionExecutingContext);


            // Assert
            expectedExecutionResult.Should().Equals(actionExecutingContext.Result);
        }


    }
}
