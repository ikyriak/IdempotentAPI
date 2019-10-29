using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Distributed;
using System;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Mvc.Filters;
using System.Linq;
using System.Reflection;
using IdempotentAPI.Helpers;

namespace IdempotentAPI.Core
{
    public class Idempotency
    {
        private readonly string _headerKeyName;
        private readonly string _distributedCacheKeysPrefix;
        private readonly IDistributedCache _distributedCache;
        private readonly int _expireHours;


        private string _idempotencyKey = string.Empty;
        private string DistributedCacheKey
        {
            set
            {
                _idempotencyKey = value;
            }
            get
            {
                return _distributedCacheKeysPrefix + _idempotencyKey;
            }
        }
        private bool _isPreIdempotencyApplied = false;
        private bool _isPreIdempotencyCacheReturned = false;
        private HashAlgorithm _hashAlgorithm = new SHA256CryptoServiceProvider();


        public Idempotency(
                    IDistributedCache distributedCache,
                    int expireHours,
                    string headerKeyName,
                    string distributedCacheKeysPrefix)
        {
            _distributedCache = distributedCache;
            _expireHours = expireHours;
            _headerKeyName = headerKeyName;
            _distributedCacheKeysPrefix = distributedCacheKeysPrefix;
        }


        private bool TryGetIdempotencyKey(HttpRequest httpRequest, out string idempotencyKey, out IActionResult errorActionResult)
        {
            idempotencyKey = string.Empty;
            errorActionResult = null;

            // The "headerKeyName" must be provided as a Header:
            if (!httpRequest.Headers.ContainsKey(_headerKeyName))
            {
                throw new ArgumentNullException(_headerKeyName, "The Idempotency header key is not found.");
            }

            Microsoft.Extensions.Primitives.StringValues idempotencyKeys;
            if (!httpRequest.Headers.TryGetValue(_headerKeyName, out idempotencyKeys))
            {
                throw new ArgumentException("The Idempotency header key value is not found.", _headerKeyName);
            }

            if (idempotencyKeys.Count > 1)
            {
                throw new ArgumentException("Multiple Idempotency keys were found.", _headerKeyName);
            }

            if (idempotencyKeys.Count <= 0
                || string.IsNullOrEmpty(idempotencyKeys.First()))
            {
                throw new ArgumentNullException(_headerKeyName, "An Idempotency header value is not found.");
            }            

            idempotencyKey = idempotencyKeys.ToString();
            return true;
        }
        private bool canPerformIdempotency(HttpRequest httpRequest)
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
                Console.WriteLine($"IdempotencyFilterAttribute [Before Controller execution]: Idempotency SKIPPED, httpRequest Method is:{httpRequest.Method.ToString()}");
                return false;
            }

            // For multiple executions of the PreStep: 
            if (_isPreIdempotencyApplied)
            {
                return false;
            }

            return true;
        }
        private byte[] generateCacheData(ResultExecutedContext context)
        {
            Dictionary<string, object> cacheData = new Dictionary<string, object>();
            // Cache Request params:
            cacheData.Add("Request.Method", context.HttpContext.Request.Method);
            cacheData.Add("Request.Path", context.HttpContext.Request.Path.HasValue ? context.HttpContext.Request.Path.Value : string.Empty);
            cacheData.Add("Request.QueryString", context.HttpContext.Request.QueryString.ToUriComponent());
            cacheData.Add("Request.DataHash", getRequestsDataHash(context.HttpContext.Request));

            //Cache Response params:
            cacheData.Add("Response.StatusCode", context.HttpContext.Response.StatusCode);
            cacheData.Add("Response.ContentType", context.HttpContext.Response.ContentType);

            Dictionary<string, List<string>> Headers = context.HttpContext.Response.Headers.ToDictionary(h => h.Key, h => h.Value.ToList());
            cacheData.Add("Response.Headers", Headers);


            // 2019-07-05: Response.Body cannot be accessed because its not yet created.
            // We are saving the Context.Result, because based on this the Response.Body is created.
            Dictionary<string, object> resultObjects = new Dictionary<string, object>();
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
                if (objectResult.Value.isAnonymousType())
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
                throw new NotImplementedException($"ApplyPostIdempotency.generateCacheData is not implement for IActionResult type {contextResult.GetType().ToString()}");
            }

            cacheData.Add("Context.Result", resultObjects);


            // Serialize & Compress data:
            return cacheData.Serialize();
        }
        private string getRequestsDataHash(HttpRequest httpRequest)
        {
            List<object> requestsData = new List<object>();

            // The Request body:
            // 2019-10-13: Use CanSeek to check if the stream does not support seeking (set position)
            if (httpRequest.ContentLength.HasValue
             && httpRequest.Body != null
                && httpRequest.Body.CanRead
                && httpRequest.Body.CanSeek
                )
            {
                using (MemoryStream memoryStream = new MemoryStream())
                {
                    httpRequest.Body.Position = 0;
                    httpRequest.Body.CopyTo(memoryStream);
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
            if (httpRequest.Form != null
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
                        using (MemoryStream memoryStream = new MemoryStream())
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

        /// <summary>
        /// Return the cached response based on the provided idempotencyKey
        /// </summary>
        /// <param name="context"></param>
        public void ApplyPreIdempotency(ActionExecutingContext context)
        {
            Console.WriteLine($"IdempotencyFilterAttribute [Before Controller execution]: Request for {context.HttpContext.Request.Method}: {context.HttpContext.Request.Path} received ({context.HttpContext.Request.ContentLength ?? 0} bytes)");

            // Check if Idempotency can be applied:
            if (!canPerformIdempotency(context.HttpContext.Request))
            {
                return;
            }

            // Try to get the IdempotencyKey value from header:
            IActionResult errorActionResult;
            if (!TryGetIdempotencyKey(context.HttpContext.Request, out _idempotencyKey, out errorActionResult))
            {
                context.Result = errorActionResult;
                return;
            }

            // Check if idempotencyKey exists in cache and return value:
            byte[] cacheDataSerialized = _distributedCache.Get(DistributedCacheKey);
            Dictionary<string, object> cacheData = (Dictionary<string, object>)cacheDataSerialized.DeSerialize();
            if (cacheData != null)
            {
                // 2019-07-06: Evaluate the "Request.DataHash" in order to be sure that the cached response is returned
                //  for the same combination of IdempotencyKey and Request
                string cachedRequestDataHash = cacheData["Request.DataHash"].ToString();
                string currentRequestDataHash = getRequestsDataHash(context.HttpContext.Request);
                if (cachedRequestDataHash != currentRequestDataHash)
                {
                    context.Result = new BadRequestObjectResult($"The Idempotency header key value '{_idempotencyKey}' was used in a different request.");
                    return;
                }

                // Set the StatusCode and Response result (based on the IActionResult type)
                // The response body will be created from a .NET middle-ware in a following step.
                int ResponseStatusCode = Convert.ToInt32(cacheData["Response.StatusCode"]);

                Dictionary<string, object> resultObjects = (Dictionary<string, object>)cacheData["Context.Result"];
                Type contextResultType = Type.GetType(resultObjects["ResultType"].ToString());
                if (contextResultType == null)
                {
                    throw new NotImplementedException($"ApplyPreIdempotency, ResultType {resultObjects["ResultType"].ToString()} is not recornized");
                }


                // Initialize the IActionResult based on its type:
                if (contextResultType == typeof(CreatedAtRouteResult))
                {
                    object value = resultObjects["ResultValue"];
                    string routeName = (string)resultObjects["ResultRouteName"];
                    Dictionary<string, string> RouteValues = (Dictionary<string, string>)resultObjects["ResultRouteValues"];

                    context.Result = new CreatedAtRouteResult(routeName, RouteValues, value);
                }
                else if (contextResultType.BaseType == typeof(ObjectResult))
                {
                    object value = resultObjects["ResultValue"];
                    ConstructorInfo ctor = contextResultType.GetConstructor(new[] { typeof(object) });
                    if (ctor != null)
                    {
                        context.Result = (IActionResult)ctor.Invoke(new object[] { value });
                    }
                    else
                    {
                        context.Result = new ObjectResult(value) { StatusCode = ResponseStatusCode };
                    }
                }
                else if (contextResultType.BaseType == typeof(StatusCodeResult)
                    || contextResultType.BaseType == typeof(ActionResult))
                {
                    ConstructorInfo ctor = contextResultType.GetConstructor(new Type[] { });
                    if (ctor != null)
                    {
                        context.Result = (IActionResult)ctor.Invoke(new object[] { });
                    }
                }
                else
                {
                    throw new NotImplementedException($"ApplyPreIdempotency is not implement for IActionResult type {contextResultType.ToString()}");
                }

                // Include cached headers (if does not exist) at the response:
                Dictionary<string, List<string>> headerKeyValues = (Dictionary<string, List<string>>)cacheData["Response.Headers"];
                if (headerKeyValues != null)
                {
                    foreach (KeyValuePair<string, List<string>> headerKeyValue in headerKeyValues)
                    {
                        if (!context.HttpContext.Response.Headers.ContainsKey(headerKeyValue.Key))
                        {
                            context.HttpContext.Response.Headers.Add(headerKeyValue.Key, headerKeyValue.Value.ToArray());
                        }
                    }
                }

                Console.WriteLine($"IdempotencyFilterAttribute [Before Controller]: Return result from idempotency cache (of type {contextResultType.ToString()})");
                _isPreIdempotencyCacheReturned = true;
            }

            Console.WriteLine($"IdempotencyFilterAttribute [Before Controller]: End");
            _isPreIdempotencyApplied = true;
        }


        /// <summary>
        /// Cache the Response in relation to the provided idempotencyKey
        /// </summary>
        /// <param name="context"></param>
        public void ApplyPostIdempotency(ResultExecutedContext context)
        {
            Console.WriteLine($"IdempotencyFilterAttribute [After Controller execution]: Response for {context.HttpContext.Response.StatusCode} sent ({context.HttpContext.Response.ContentLength ?? 0} bytes)");

            if (!_isPreIdempotencyApplied || _isPreIdempotencyCacheReturned)
            {
                Console.WriteLine($"IdempotencyFilterAttribute [After Controller execution]: Result NOT cached");
                return;
            }

            // Generate the data to be cached
            byte[] cacheDataSerialized = generateCacheData(context);

            // Set the expiration of the cache:
            DistributedCacheEntryOptions cacheOptions = new DistributedCacheEntryOptions();
            cacheOptions.AbsoluteExpirationRelativeToNow = new TimeSpan(_expireHours, 0, 0);

            // Save to cache:
            _distributedCache.Set(DistributedCacheKey, cacheDataSerialized, cacheOptions);

            Console.WriteLine($"IdempotencyFilterAttribute [After Controller execution]: Result is cached for idempotencyKey: {_idempotencyKey}");
        }
    }
}