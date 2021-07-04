# Idempotent API <sup>BETA</sup>

A [distributed system](https://en.wikipedia.org/wiki/Distributed_computing) consists of multiple components located on different networked computers, which communicate and coordinate their actions by passing messages to one another from any system. For example, I am sure that you have heard of the [microservices](https://microservices.io/) architecture, which is a kind of distributed system.

Creating Web APIs for distributed systems is challenging because of distribution pitfalls such as process failures, communication failures, asynchrony, and concurrency. When building [fault-tolerant distributed applications](https://www.microsoft.com/en-us/research/publication/fault-tolerance-via-idempotence/), one common requirement and challenge is the need to be idempotent.

- In mathematics and computer science, an operation is [idempotent](https://en.wikipedia.org/wiki/Idempotence) when applied multiple times without changing the result beyond the initial application.
- [Fault-tolerant](https://en.wikipedia.org/wiki/Software_fault_tolerance) applications can continue operating despite the system, hardware, and network faults of one or more components, ensuring [high availability](https://en.wikipedia.org/wiki/High_availability_software) and [business continuity](https://en.wikipedia.org/wiki/Business_continuity_planning) for critical applications or systems.

Idempotence in Web APIs ensures that the API works correctly (as designed) even when consumers (clients) send the same request multiple times. For example, this case can happen when the API failed to generate the response (due to process failures, temporary downtime, etc.) or because the response was generated but could not be transferred (network issues).

Imagine a scenario in which the user clicks a “Pay” button to make a purchase. For unknown reasons, the user receives an error, but the payment was completed. If the user clicks the “Pay” button again or the request is re-sent by a retry library, we would result in two payments! Using idempotency, the user will get a successful message (e.g., on the second try), but only one charge would be performed.

Creating Idempotent Web APIs is the first step before using a resilient and transient-fault-handling library, such as [Polly](https://github.com/App-vNext/Polly). The Polly .NET library allows developers to express policies such as Retry, Circuit Breaker, Timeout, Bulkhead Isolation, and Fallback in a fluent and thread-safe manner.

This article presents an effort to create the IdempotentAPI library, which provides an easy way to develop idempotent Web APIs. In the following sections, we will see the idempotency in the different HTTP methods, how the IdempotentAPI library works, its code, and finally, how to use the IdempotentAPI NuGet package.



## Idempotency in HTTP (Web)

HTTP defines a set of request [methods](https://developer.mozilla.org/en-US/docs/Web/HTTP/Methods) (HTTP verbs: GET, POST, PUT, PATCH, etc.) to indicate the desired action to be performed for a given resource. An idempotent HTTP method can be called many times without resulting in different outcomes. Safe methods are HTTP methods that do not modify the resources. In Table 1, we can see details about which HTTP methods are idempotent or/and safe.

Table 1. - Idempotent or/and Safe HTTP methods (verbs).

| **HTTP Method** | **Idempotent** | **Safe** | **Description**                                              |
| --------------- | -------------- | -------- | ------------------------------------------------------------ |
| GET             | Yes            | Yes      | Safe HTTP methods do not modify resources. Thus, multiple calls  with this method will always return the same response. |
| OPTIONS         | Yes            | Yes      | Same as the previous HTTP method.                            |
| HEAD            | Yes            | Yes      | Same as the previous HTTP method.                            |
| PUT             | Yes            | No       | The PUT HTTP method is idempotent because calling this HTTP  method multiple times (with the same request data) will update the same  resource and not change the outcome. |
| DELETE          | Yes            | No       | The DELETE HTTP method is idempotent because calling this HTTP  method multiple times will only delete the resource once. Thus, numerous  calls of the DELETE HTTP method will not change the outcome. |
| POST            | **No**         | No       | Calling the POST method multiple times can have different  results and will create multiple resources. For that reason, the POST method  is **not**  idempotent. |
| PATCH           | **No**         | No       | The PATCH method can be idempotent depending on the  implementation, but it isn’t required to be. For that reason, the PATCH  method is **not**  idempotent. |

 

The creation of an `idempotent consumer` is an essential factor in HTTP idempotency. The API server would need a way to recognize subsequent retries of the same request. Commonly, the consumer generates a unique value, called [idempotency-key](https://tools.ietf.org/id/draft-idempotency-header-01.html#section-2), which the API server uses for that purpose. In addition, when building an idempotent consumer, it is recommended to:

- Use "[V4 UUIDs](https://en.wikipedia.org/wiki/Universally_unique_identifier)" for the creation of the idempotency unique keys (e.g. “07cd2d27-e0dc-466f-8193-28453e9c3023”).
- Use techniques like the [exponential backoff and random jitter](https://aws.amazon.com/blogs/architecture/exponential-backoff-and-jitter/), i.e., including an exponential and random delay between continuous requests.

 

## The IdempotentAPI Library

The IdempotentAPI is an open-source NuGet library, which implements an ASP.NET Core attribute ([filter](https://www.dotnetnakama.com/blog/creating-and-testing-asp-dotnet-core-filter-attributes/)) to handle the HTTP write operations (POST and PATCH) that can affect only once for the given request data and idempotency-key.

### How IdempotentAPI Works

The API consumer (e.g., a Front-End website) sends a request including an Idempotency-Key header unique identifier (default name: IdempotencyKey). The API server checks if that unique identifier has been used previously for that request and either returns the cached response (without further execution) or save-cache the response along with the unique identifier. The cached response includes the HTTP status code, the response body, and headers.

Storing data is necessary for idempotency, but if the data are not expired after a certain period, it will include unneeded complexity in data storage, security, and scaling. Therefore, the data should have a retention period that makes sense for your problem domain.

The IdempotentAPI library performs additional validation of the request’s hash-key to ensure that the cached response is returned for the same combination of Idempotency-Key and Request to prevent accidental misuse.

The following figure shows an example of the IdempotentAPI library flow for two exact POST requests. As shown, the IdempotentAPI library includes two additional steps, one before the controller’s execution and one after constructing the controller’s response.

![An example of handling two exact POST requests.](./etc/IdempotentAPI_FlowExample.png)
 

### The Source Code

Let’s have a quick look at the projects and the main code files.

- **/Core/Idempotency.cs**: The core implementation containing the idempotency logic applied before and after the request’s execution.
- **/Filters/IdempotencyAttributeFilter.cs**: The filter implementation (of `IActionFilter` and `IResultFilter`) uses the core implementation on specific steps of the filter pipeline execution flow.
- **/Filters/IdempotencyAttribute.cs**: The idempotency attribute implementation for its input options and to initialize the idempotency filter.
- **/Helpers/Utils.cs**: Several helper static functions for serialization, hashing, and compression.



## IdempotentAPI NuGet package

The IdempotentAPI library is available as a [NuGet package](https://www.nuget.org/packages/IdempotentAPI/). This section shows how we could use the NuGet package in a Web API project. For more examples, you can check the [sample projects](https://github.com/ikyriak/IdempotentAPI/tree/master/samples).

### Step 1: Register the Distributed Cache (as Persistent Storage)

As we have seen, storing-caching data is necessary for idempotency. Therefore, the IdempotentAPI library needs an implementation of the `IDistributedCache` to be registered in the `Startup.ConfigureServices` method (such as Memory Cache, SQL Server cache, Redis cache, etc.). For more details about the available framework-provided implementations, see the [Distributed caching in the ASP.NET Core](https://docs.microsoft.com/en-us/aspnet/core/performance/caching/distributed?view=aspnetcore-5.0#establish-distributed-caching-services) article.

```c#
// We could use a memory cache for development purposes.
services.AddDistributedMemoryCache();
```

### Step 2: Decorate Response Classes as Serializable

The response Data Transfer Objects (DTOs) need to be serialized before caching. For that reason, we will have to decorate the relative DTOs as `[Serializable]`. For example, see the code below.

```c#
using System;

namespace WebApi_3_1.DTOs
{
    [Serializable]
    public class SimpleResponse
    {
        public int Id { get; set; }
        public string Message { get; set; }
        public DateTime CreatedOn { get; set; }
    }
}
```



### Step 3: Set Controller Operations as Idempotent

In your `Controller` class, add the following using statement. Then choose which operations should be Idempotent by setting the `[Idempotent()]` attribute, either on the controller’s class or on each action separately. The following two sections describe these two cases.

```c#
using IdempotentAPI.Filters;
```

#### Using the Idempotent Attribute on a Controller’s Class

By using the Idempotent attribute on the API Controller’s Class, `all` the POST and PATCH actions will work as idempotent operations (requiring the IdempotencyKey header).

```c#
[ApiController]
[Route("[controller]")]
[Consumes("application/json")]
[Produces("application/json")]
[Idempotent(Enabled = true)]
public class SimpleController : ControllerBase
{
    // ...
}
```

 

#### Using the Idempotent Attribute on a Controller’s Action

By using the Idempotent attribute on each action (HTTP POST or PATCH), we can choose which of them should be Idempotent. In addition, we could use the Idempotent attribute to set different options per action.

```c#
[HttpPost]
[Idempotent(ExpireHours = 48)]
ublic IActionResult Post([FromBody] SimpleRequest simpleRequest)
{
    // ...
}
```

 

### Idempotent Attribute Options

The Idempotent attribute provides a list of options, as shown in the following table.

Table 2. - Idempotent attribute options

| **Name**                   | **Type** | **Default Value** | **Description**                                              |
| -------------------------- | -------- | ----------------- | ------------------------------------------------------------ |
| Enabled                    | bool     | true              | Enable or Disable the Idempotent operation on an API Controller’s  class or method. |
| ExpireHours                | int      | 24                | The retention period (in hours) of the idempotent cached data. |
| HeaderKeyName              | string   | IdempotencyKey    | The name of the Idempotency-Key header.                      |
| DistributedCacheKeysPrefix | string   | IdempAPI_         | A prefix for the DistributedCache key names.                 |

 

## Summary

A distributed system consists of multiple components located on different networked computers, which communicate and coordinate their actions by passing messages to one another from any system. Fault-tolerant applications can continue operating despite the system, hardware, and network faults of one or more components.

Idempotence in Web APIs ensures that the API works correctly (as designed) even when consumers (clients) send the same request multiple times. Applying idempotency in your APIs is the first step before using a resilient and transient-fault-handling library, such as [Polly](https://github.com/App-vNext/Polly).

The `IdempotentAPI` is an open-source [NuGet library](https://www.nuget.org/packages/IdempotentAPI/), which implements an ASP.NET Core attribute (filter) to handle the HTTP write operations (POST and PATCH) that can affect only once for the given request data and idempotency-key. In this article, we have seen how the IdempotentAPI library works, its code, and finally, how to use the IdempotentAPI NuGet package.

For ensuring high availability and business continuity for critical Web APIs, the `IdempotentAPI` library is your first step 😉. IdempotentAPI is open-source (MIT licensed)! So, any help in coding, suggestions, sharing this article, giving a GitHub Star, etc., are welcome.


## License
The IdempotentAPI is [MIT licensed](./LICENSE.md).