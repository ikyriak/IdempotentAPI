#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Reflection.PortableExecutable;
using System.Security.Cryptography;
using IdempotentAPI.AccessCache;
using IdempotentAPI.AccessCache.Exceptions;
using IdempotentAPI.Core;
using IdempotentAPI.Extensions;
using IdempotentAPI.Helpers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Primitives;
using Newtonsoft.Json;

namespace IdempotentAPI.Core
{
    public class Idempotency
    {
        private readonly object _cacheEntryOptions;
        private readonly IIdempotencyAccessCache _distributedCache;
        private readonly IIdempotencySettings _settings;
        private readonly IKeyGenerator _keyGenerator;

        /// <summary>
        /// The read-only list of HTTP Header Keys will be handled from the selected HTTP Server and
        /// not included in the cache.
        /// </summary>
        private readonly IReadOnlyList<string> _excludeHttpHeaderKeys = new List<string>() { "Transfer-Encoding" };

        private readonly HashAlgorithm _hashAlgorithm;
        private readonly ILogger<Idempotency>? _logger;
        private string _idempotencyKey = string.Empty;

        private bool _isPreIdempotencyApplied;
        private bool _isPreIdempotencyCacheReturned;
        private string _distributedCacheKey;

        public Idempotency(IIdempotencyAccessCache distributedCache,
            IIdempotencySettings settings,
            IKeyGenerator keyGenerator,
            ILogger<Idempotency> logger)
        {
            _distributedCache = distributedCache ?? throw new ArgumentNullException($"An {nameof(IIdempotencyAccessCache)} is not configured. You should register the required services by using the \"AddIdempotentAPIUsing{{YourCacheProvider}}\" function.");
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _keyGenerator = keyGenerator ?? throw new ArgumentNullException(nameof(keyGenerator));
            _logger = logger;

            _hashAlgorithm = SHA256.Create();
            _cacheEntryOptions = _distributedCache.CreateCacheEntryOptions(settings.ExpiryTime);
        }

        /// <summary>
        /// Cache the Response in relation to the provided idempotencyKey
        /// </summary>
        /// <param name="context"></param>
        public void ApplyPostIdempotency(ResultExecutedContext context)
        {
            if (IsLoggerEnabled(LogLevel.Information))
            {
                _logger.LogInformation("IdempotencyFilterAttribute [After Controller execution]: Response for {HttpContextResponseStatusCode} sent ({HttpContextResponseContentLength} bytes)", context.HttpContext.Response.StatusCode, context.HttpContext.Response.ContentLength ?? 0);
            }

            if (!_isPreIdempotencyApplied || _isPreIdempotencyCacheReturned)
            {
                if (IsLoggerEnabled(LogLevel.Information))
                {
                    _logger.LogInformation("IdempotencyFilterAttribute [After Controller execution]: SKIPPED (isPreIdempotencyApplied:{_isPreIdempotencyApplied}, isPreIdempotencyCacheReturned:{_isPreIdempotencyCacheReturned})", _isPreIdempotencyApplied, _isPreIdempotencyCacheReturned);
                }
                return;
            }

            // Return when the current response is unsuccessful, and we should accept only the success status codes.
            if (_settings.CacheOnlySuccessResponses
                && !context.HttpContext.Response.IsSuccessStatusCode())
            {
                try
                {
                    _distributedCache.Remove(_distributedCacheKey, _settings.DistributedLockTimeout);
                }
                catch (DistributedLockNotAcquiredException distributedLockNotAcquiredException)
                {
                    LogDistributedLockNotAcquiredException("After Controller execution", distributedLockNotAcquiredException);
                }

                if (IsLoggerEnabled(LogLevel.Information))
                {
                    _logger.LogInformation("IdempotencyFilterAttribute [After Controller execution]: SKIPPED (Status Code {StatusCode} is not success 2xx).", context.HttpContext.Response.StatusCode);
                }
                return;
            }

            // Generate the data to be cached
            byte[]? cacheDataBytes = GenerateCacheData(context);

            // Save to cache:
            try
            {
                _distributedCache.Set(_distributedCacheKey, cacheDataBytes, _cacheEntryOptions, _settings.DistributedLockTimeout);
            }
            catch (DistributedLockNotAcquiredException distributedLockNotAcquiredException)
            {
                LogDistributedLockNotAcquiredException("After Controller execution", distributedLockNotAcquiredException);
            }

            if (IsLoggerEnabled(LogLevel.Information))
            {
                _logger.LogInformation("IdempotencyFilterAttribute [After Controller execution]: Result is cached for idempotencyKey: {idempotencyKey}", _idempotencyKey);
            }
        }

        /// <summary>
        /// Return the cached response based on the provided idempotencyKey
        /// </summary>
        /// <param name="context"></param>
        public void ApplyPreIdempotency(ActionExecutingContext context)
        {
            if (IsLoggerEnabled(LogLevel.Information))
            {
                _logger.LogInformation("IdempotencyFilterAttribute [Before Controller execution]: Request for {HttpContextRequestMethod}: {HttpContextRequestPath} received ({HttpContextRequestContentLength} bytes)", context.HttpContext.Request.Method, context.HttpContext.Request.Path, context.HttpContext.Request.ContentLength ?? 0);
            }

            // Check if Idempotency can be applied:
            if (!CanPerformIdempotency(context.HttpContext.Request))
            {
                return;
            }

            // Try to get the IdempotencyKey value from header:
            if (!TryGetIdempotencyKey(context.HttpContext.Request, out _idempotencyKey))
            {
                context.Result = null;
                return;
            }

            var controller = context.RouteData.Values.ContainsKey("controller") ? context.RouteData.Values["controller"]?.ToString() ?? string.Empty : string.Empty;
            var action = context.RouteData.Values.ContainsKey("action") ? context.RouteData.Values["action"]?.ToString() ?? string.Empty : string.Empty;
            _distributedCacheKey = _keyGenerator.Generate(_settings.DistributedCacheKeysPrefix, controller, action, _idempotencyKey);

            // Check if idempotencyKey exists in cache and return value:
            string uniqueRequestId = GetRequestId(context.HttpContext.Request);
            byte[] cacheDataBytes;

            try
            {
                cacheDataBytes = _distributedCache.GetOrSet(
                    _distributedCacheKey,
                    defaultValue: GenerateRequestInFlightCacheData(uniqueRequestId),
                    options: _cacheEntryOptions,
                    distributedLockTimeout: _settings.DistributedLockTimeout);
            }
            catch (DistributedLockNotAcquiredException distributedLockNotAcquiredException)
            {
                LogDistributedLockNotAcquiredException("Before Controller", distributedLockNotAcquiredException);

                context.Result = ResultOnDistributedLockNotAcquired(context, distributedLockNotAcquiredException);
                return;
            }

            IReadOnlyDictionary<string, object>? cacheData = cacheDataBytes.DeSerialize<IReadOnlyDictionary<string, object>>();
            if (cacheData is null)
            {
                throw new Exception("Cannot DeSerialize cached data.");
            }

            // RPG - 2021-07-05 - Check if there is a copy of this request in flight,
            // if so return a 409 Http Conflict response.
            if (cacheData.ContainsKey("Request.Inflight")
                && uniqueRequestId.ToLower() != cacheData["Request.Inflight"].ToString().ToLower())
            {
                context.Result = CreateResponse(context, HttpStatusCode.Conflict, null);
                return;
            }

            if (!cacheData.ContainsKey("Request.Inflight"))
            {
                context.HttpContext.Response.Headers.Add(_settings.RequestIdHeader, uniqueRequestId);

                // 2019-07-06: Evaluate the "Request.DataHash" in order to be sure that the cached
                // response is returned for the same combination of IdempotencyKey and Request
                string cachedRequestDataHash = cacheData["Request.DataHash"].ToString();
                string currentRequestDataHash = GetRequestsDataHash(context.HttpContext.Request);
                if (cachedRequestDataHash != currentRequestDataHash)
                {
                    context.Result = CreateResponse(context, HttpStatusCode.BadRequest, $"The Idempotency header key value '{_idempotencyKey}' was used in a different request.");
                    return;
                }

                // Set the StatusCode and Response result (based on the IActionResult type)
                // The response body will be created from a .NET middle-ware in a following step.
                int responseStatusCode = Convert.ToInt32(cacheData["Response.StatusCode"]);

                Dictionary<string, object> resultObjects = (Dictionary<string, object>)cacheData["Context.Result"];
                Type contextResultType = Type.GetType(resultObjects["ResultType"].ToString());
                if (contextResultType == null)
                {
                    throw new NotImplementedException($"ApplyPreIdempotency, ResultType {resultObjects["ResultType"]} is not recognized");
                }


                // Initialize the IActionResult based on its type:
                if (contextResultType == typeof(CreatedAtRouteResult))
                {
                    object value = resultObjects["ResultValue"];
                    string routeName = (string)resultObjects["ResultRouteName"];
                    Dictionary<string, string> RouteValues = (Dictionary<string, string>)resultObjects["ResultRouteValues"];

                    context.Result = new CreatedAtRouteResult(routeName, RouteValues, value);
                }
                else if (contextResultType.BaseType == typeof(ObjectResult)
                    || contextResultType == typeof(ObjectResult))
                {
                    object value = resultObjects["ResultValue"];
                    ConstructorInfo ctor = contextResultType.GetConstructor(new[] { typeof(object) });
                    if (ctor != null && ctor.DeclaringType != typeof(ObjectResult))
                    {
                        context.Result = (IActionResult)ctor.Invoke(new object[] { value });
                    }
                    else
                    {
                        context.Result = new ObjectResult(value) { StatusCode = responseStatusCode };
                    }
                }
                else if (contextResultType.BaseType == typeof(StatusCodeResult)
                    || contextResultType.BaseType == typeof(ActionResult))
                {
                    ConstructorInfo ctor = contextResultType.GetConstructor(Array.Empty<Type>());
                    if (ctor != null)
                    {
                        context.Result = (IActionResult)ctor.Invoke(Array.Empty<object>());
                    }
                }
                else
                {
                    throw new NotImplementedException($"ApplyPreIdempotency is not implemented for IActionResult type {contextResultType}");
                }

                // Include cached headers (if does not exist) at the response:
                Dictionary<string, List<string>> headerKeyValues = (Dictionary<string, List<string>>)cacheData["Response.Headers"];
                if (headerKeyValues != null)
                {
                    foreach (KeyValuePair<string, List<string>> headerKeyValue in headerKeyValues)
                    {
                        if (headerKeyValue.Key.Equals("Content-Type"))
                            continue;

                        if (headerKeyValue.Key.Equals(_settings.RequestIdHeader))
                        {
                            context.HttpContext.Response.Headers.Add(_settings.OriginalRequestIdHeader, headerKeyValue.Value.ToArray());
                        }
                        else if (!context.HttpContext.Response.Headers.ContainsKey(headerKeyValue.Key))
                        {
                            context.HttpContext.Response.Headers.Add(headerKeyValue.Key, headerKeyValue.Value.ToArray());
                        }
                    }
                }

                if (IsLoggerEnabled(LogLevel.Information))
                {
                    _logger.LogInformation("IdempotencyFilterAttribute [Before Controller]: Return result from idempotency cache (of type {contextResultType})", contextResultType.ToString());
                }
                _isPreIdempotencyCacheReturned = true;
            }

            if (IsLoggerEnabled(LogLevel.Information))
            {
                _logger.LogInformation("IdempotencyFilterAttribute [Before Controller]: End");
            }
            _isPreIdempotencyApplied = true;
        }

        /// <summary>
        /// Cancel the idempotency by removing the related cached data. For example, this function
        /// can be used when exceptions occur.
        /// </summary>
        public void CancelIdempotency()
        {
            try
            {
                _distributedCache.Remove(_distributedCacheKey, _settings.DistributedLockTimeout);
            }
            catch (DistributedLockNotAcquiredException distributedLockNotAcquiredException)
            {
                LogDistributedLockNotAcquiredException("After Controller execution", distributedLockNotAcquiredException);
            }

            if (IsLoggerEnabled(LogLevel.Information))
            {
                _logger.LogInformation("IdempotencyFilterAttribute [After Controller execution]: SKIPPED (An exception occurred).");
            }
        }

        protected virtual string GetRequestId(HttpRequest request)
        {
            request.Headers.TryGetValue(_settings.RequestIdHeader, out var value);
            value = value.FirstOrDefault() ?? Guid.NewGuid().ToString();
            return value.ToString();
        }

        protected virtual IActionResult ResultOnDistributedLockNotAcquired(ActionExecutingContext context, DistributedLockNotAcquiredException exception)
        {
            return new ConflictResult();
        }

        protected virtual IActionResult CreateResponse(ActionExecutingContext context, HttpStatusCode status, object error) =>
            status switch
            {
                HttpStatusCode.Conflict => new ConflictObjectResult(error),
                HttpStatusCode.BadRequest => new BadRequestObjectResult(error),
                _ => new StatusCodeResult((int)status)
            };

        private bool CanPerformIdempotency(HttpRequest httpRequest)
        {
            // If distributedCache is not configured
            if (_distributedCache == null)
            {
                throw new Exception("An IDistributedCache is not configured.");
            }

            // Idempotency is applied on Post & Patch Http methods:
            if (httpRequest.Method != HttpMethods.Post
                && httpRequest.Method != HttpMethods.Patch)
            {
                if (IsLoggerEnabled(LogLevel.Information))
                {
                    _logger.LogInformation("IdempotencyFilterAttribute [Before Controller execution]: Idempotency SKIPPED, httpRequest Method is: {httpRequestMethod}", httpRequest.Method.ToString());
                }

                return false;
            }

            // For multiple executions of the PreStep:
            if (_isPreIdempotencyApplied)
            {
                return false;
            }

            return true;
        }

        private byte[] GenerateCacheData(ResultExecutedContext context)
        {
            Dictionary<string, object> cacheData = new();
            // Cache Request params:
            cacheData.Add("Request.Method", context.HttpContext.Request.Method);
            cacheData.Add("Request.Path", context.HttpContext.Request.Path.HasValue ? context.HttpContext.Request.Path.Value : string.Empty);
            cacheData.Add("Request.QueryString", context.HttpContext.Request.QueryString.ToUriComponent());
            cacheData.Add("Request.DataHash", GetRequestsDataHash(context.HttpContext.Request));

            //Cache Response params:
            cacheData.Add("Response.StatusCode", context.HttpContext.Response.StatusCode);
            cacheData.Add("Response.ContentType", context.HttpContext.Response.ContentType);

            Dictionary<string, List<string>> Headers = context.HttpContext.Response.Headers
                .Where(h => !_excludeHttpHeaderKeys.Contains(h.Key))
                .ToDictionary(h => h.Key, h => h.Value.ToList());

            cacheData.Add("Response.Headers", Headers);


            // 2019-07-05: Response.Body cannot be accessed because its not yet created.
            // We are saving the Context.Result, because based on this the Response.Body is created.
            Dictionary<string, object> resultObjects = new();
            var contextResult = context.Result;
            resultObjects.Add("ResultType", contextResult.GetType().AssemblyQualifiedName);

            if (contextResult is CreatedAtRouteResult route)
            {
                //CreatedAtRouteResult.CreatedAtRouteResult(string routeName, object routeValues, object value)
                resultObjects.Add("ResultValue", route.Value);
                resultObjects.Add("ResultRouteName", route.RouteName);

                Dictionary<string, string> RouteValues = route.RouteValues.ToDictionary(r => r.Key, r => r.Value.ToString());
                resultObjects.Add("ResultRouteValues", RouteValues);
            }
            else if (contextResult is ObjectResult objectResult)
            {
                if (objectResult.Value.IsAnonymousType())
                {
                    resultObjects.Add("ResultValue", Utils.AnonymousObjectToDictionary(objectResult.Value, Convert.ToString));
                }
                else
                {
                    resultObjects.Add("ResultValue", objectResult.Value);
                }
            }
            else if (contextResult is StatusCodeResult || contextResult is ActionResult)
            {
                // Known types that do not need additional data
            }
            else
            {
                throw new NotImplementedException($"ApplyPostIdempotency.generateCacheData is not implement for IActionResult type {contextResult.GetType()}");
            }

            cacheData.Add("Context.Result", resultObjects);


            byte[]? serializedCacheData = cacheData.Serialize();

            if (serializedCacheData is null)
                throw new Exception("Cannot Serialize the inFlightCacheData.");

            return serializedCacheData;
        }

        private byte[] GenerateRequestInFlightCacheData(string value)
        {
            Dictionary<string, object> inFlightCacheData = new()
            {
                { "Request.Inflight", value }
            };

            byte[]? serializedCacheData = inFlightCacheData.Serialize();
            if (serializedCacheData is null)
                throw new Exception("Cannot Serialize the inFlightCacheData.");

            return serializedCacheData;
        }

        private string GetRequestsDataHash(HttpRequest httpRequest)
        {
            List<object> requestsData = new();

            // The Request body:
            // 2019-10-13: Use CanSeek to check if the stream does not support seeking (set position)
            if (httpRequest.ContentLength.HasValue
                && httpRequest.Body != null)
            {
                // 2022-08-18: Enable buffering for large body requests and then read the buffer asynchronously.
                httpRequest.EnableBuffering();

                if (httpRequest.Body.CanRead
                    && httpRequest.Body.CanSeek)
                {
                    using MemoryStream memoryStream = new();
                    httpRequest.Body.Position = 0;

                    var copyTask = httpRequest.Body.CopyToAsync(memoryStream);
                    copyTask.Wait();

                    requestsData.Add(memoryStream.ToArray());
                }
            }

            // The Form data:
            if (httpRequest.HasFormContentType
                && httpRequest.Form != null)
            {
                requestsData.Add(httpRequest.Form);
            }

            // Form-Files data:
            if (httpRequest.HasFormContentType
                && httpRequest.Form != null
                && httpRequest.Form.Files != null
                && httpRequest.Form.Files.Count > 0)
            {
                foreach (IFormFile formFile in httpRequest.Form.Files)
                {
                    Stream fileStream = formFile.OpenReadStream();
                    if (fileStream.CanRead
                        && fileStream.CanSeek
                        && fileStream.Length > 0)
                    {
                        using (MemoryStream memoryStream = new())
                        {
                            fileStream.Position = 0;
                            fileStream.CopyTo(memoryStream);
                            requestsData.Add(memoryStream.ToArray());
                        }
                    }
                    else
                    {
                        requestsData.Add(formFile.FileName + "_" + formFile.Length.ToString() + "_" + formFile.Name);
                    }
                }
            }

            // The request's URL:
            if (httpRequest.Path.HasValue)
            {
                requestsData.Add(httpRequest.Path.ToString());
            }

            return Utils.GetHash(_hashAlgorithm, JsonConvert.SerializeObject(requestsData));
        }

        private bool IsLoggerEnabled(LogLevel logLevel)
        {
            return _logger?.IsEnabled(logLevel) ?? false;
        }

        private void LogDistributedLockNotAcquiredException(
            string message,
            DistributedLockNotAcquiredException distributedLockNotAcquiredException)
        {
            if (IsLoggerEnabled(LogLevel.Error) && distributedLockNotAcquiredException.InnerException is not null)
            {
                _logger.LogError(
                    distributedLockNotAcquiredException.InnerException,
                    $"IdempotencyFilterAttribute [{message}]: DistributedLockNotAcquired. {distributedLockNotAcquiredException.Message}");
            }
            else if (IsLoggerEnabled(LogLevel.Warning))
            {
                _logger.LogWarning($"IdempotencyFilterAttribute [{message}]: DistributedLockNotAcquired. {distributedLockNotAcquiredException.Message}");
            }
        }

        private bool TryGetIdempotencyKey(HttpRequest httpRequest, out string idempotencyKey)
        {
            idempotencyKey = string.Empty;

            // The "headerKeyName" must be provided as a Header:
            if (!httpRequest.Headers.ContainsKey(_settings.HeaderKeyName))
            {
                throw new ArgumentNullException(_settings.HeaderKeyName, "The Idempotency header key is not found.");
            }

            if (!httpRequest.Headers.TryGetValue(_settings.HeaderKeyName, out StringValues idempotencyKeys))
            {
                throw new ArgumentException("The Idempotency header key value is not found.", _settings.HeaderKeyName);
            }

            if (idempotencyKeys.Count > 1)
            {
                throw new ArgumentException("Multiple Idempotency keys were found.", _settings.HeaderKeyName);
            }

            if (idempotencyKeys.Count <= 0
                || string.IsNullOrEmpty(idempotencyKeys.First()))
            {
                throw new ArgumentNullException(_settings.HeaderKeyName, "An Idempotency header value is not found.");
            }

            idempotencyKey = idempotencyKeys.ToString();
            return true;
        }
    }
}