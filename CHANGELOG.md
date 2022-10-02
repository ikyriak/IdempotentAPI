# Changelog
All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/), and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).



## [2.0.0-RC.1] - 2022-10-02 - BREAKING

### Fixed

- ✅ There were two cases in which the IdempotentAPI didn't respond as expected. For that reason, we made some corrections and improvements. Thanks to [@kvuong](https://github.com/khoavn) for reporting this issue ([#37](https://github.com/ikyriak/IdempotentAPI/issues/37)) 💪🙏.

  - When the controller returns a non-successful response (4xx, 5xx,  etc.), the IdempotentAPI cache the error response. In some cases, maybe we would like this behavior. For that reason, we have added the `CacheOnlySuccessResponses` attribute option to set it per case (default value: `True`).
  - When an exception occurs in the controller, the IdempotentAPI stack is in the in-flight mode by returning a `409 Conflict` response in the subsequent requests. Thus, we have made a fix to remove the in-flight request on exceptions.
  - However, as long as a request is in inflight mode (running), all other requests will still get a `409 Conflict` response. For that reason, we should be careful when configuring the request timeout.
- ✅ There was a bug when a request body was big enough (e.g., 30kb+). The cache couldn't appropriately be fetched because of a different hash string. Thanks, [@bernardiego](https://github.com/bernardiego), for taking the time to report and provide a fix for this issue ([#38](https://github.com/ikyriak/IdempotentAPI/issues/38)) 🙏❤.
- ✅ Fix a bug in the reconstruction of the `ObjectResult` responses. Thanks to [@MohamadTahir](https://github.com/MohamadTahir) for reporting this issue ([#39](https://github.com/ikyriak/IdempotentAPI/issues/39)) and providing a workaround 🙏.

### Added

- ❗ The `CacheOnlySuccessResponses` attribute option is included (default value: `True`) to cache only 2xx HTTP responses.

- 🌟 Support idempotency in a **Cluster Environment** (i.e., a group of multiple server instances) using **Distributed Locks**. Refactoring has been performed to include additional abstractions and distinguish the Caching (`IIdempotencyCache`), Distributed Locks (`IDistributedAccessLockProvider`), and Accessing of them (`IIdempotencyAccessCache`). Thanks to [@Rast1234](https://github.com/Rast1234) for showing the need for this feature 💪🙏. Currently, we support the following two implementations. 

  - 🌟 The `DistributedLockTimeoutMilli` attribute option is included to configure the time the distributed lock will wait for the lock to be acquired (in milliseconds).

  |                                                              | Supported Technologies                                       | DI Registration                                              |
  | ------------------------------------------------------------ | ------------------------------------------------------------ | ------------------------------------------------------------ |
  | [samcook/RedLock.net](https://github.com/samcook/RedLock.net) | [Redis Redlock](https://redis.io/docs/reference/patterns/distributed-locks/) | `services.AddRedLockNetDistributedAccessLock(redisEndpoints);` |
  | [madelson/DistributedLock](https://github.com/madelson/DistributedLock) | Redis, SqlServer, Postgres and many [more](https://github.com/madelson/DistributedLock#implementations). | `services.AddMadelsonDistributedAccessLock();`               |

### Changed

- ❗ **IMPORTANT**: We should register the IdempotentAPI Core services.

  - ```c#
    services.AddIdempotentAPI();
    ```

- Dependency Updates

  - Update `Newtonsoft.Json` from `12.0.3` to `13.0.1`.
  - Update `Microsoft.Extensions.Caching.Abstractions` from `3.1.21` to `6.0.0`.
  - Update `Microsoft.Extensions.DependencyInjection.Abstractions` from `3.1.22` to `6.0.0`.
  - Update `ZiggyCreatures.FusionCache` from `0.1.7` to `0.13.0`.
  - Update `ZiggyCreatures.FusionCache.Serialization.NewtonsoftJson` from `0.1.7` to `0.13.0`.



## [1.0.1] - 2022-03-12

### Fixed
- Idempotency did not work as expected when the return type on the controller action was a custom object and not an `ActionResult`. ([#33](https://github.com/ikyriak/IdempotentAPI/issues/33))
- Thanks to @MohamadTahir for reporting and investigating this issue 🙏.



## [1.0.0-RC.1] - 2022-01-18 - BREAKING
### Added
- 📝 This `CHANGELOG.md` file quickly shows the notable changes we have performed between the project's releases (versions).

- 🌟 Support for ASP.NET Core 5.0 and 6.0 by stopping using the [obsolete](https://docs.microsoft.com/en-us/dotnet/core/compatibility/core-libraries/5.0/binaryformatter-serialization-obsolete) `BinaryFormatter` and using the `Newtonsoft JsonSerializer` ([recommended action](https://docs.microsoft.com/en-us/dotnet/core/compatibility/core-libraries/5.0/binaryformatter-serialization-obsolete#recommended-action)).

- 🌟 Define the `IIdempotencyCache` interface to decide which caching implementation is appropriate in each use case. Currently, we support the following two implementations. However, you can use your implementation 😉.

  |                                                | Support Concurrent Requests | Primary Cache     |      2nd-Level Cache       | Advanced features |
  | ---------------------------------------------- | :-------------------------: | ----------------- | :------------------------: | :---------------: |
  | IdempotentAPI.Cache.DistributedCache (Default) |              ✔️              | IDistributedCache |             ❌              |         ❌         |
  | IdempotentAPI.Cache.FusionCache                |              ✔️              | Memory Cache      | ✔️<br />(IDistributedCache) |         ✔️         |

- 🌟 Support the [FusionCache](https://github.com/jodydonetti/ZiggyCreatures.FusionCache), which provides high performance and robust cache with an optional distributed 2nd layer and some advanced features.

  - `FusionCache` also includes some advanced features like a **fail-safe** mechanism, **cache stampede** prevention, fine grained **soft/hard timeouts** with **background factory completion**, **extensive** customizable logging, and [more](https://github.com/jodydonetti/ZiggyCreatures.FusionCache#heavy_check_mark-features).

- ⚙ Configure the logging level of the `IdempotentAPI` logs that we would like to keep. For example, as we can see in the following JSON, we can set `IdempotentAPI.Core.Idempotency` in the `appsettings.json`.

  ```json
  {
    "Logging": {
      "LogLevel": {
        "Default": "Information",
        "IdempotentAPI.Core.Idempotency": "Warning"
      }
    }
  }
  ```

### Changed
- The Logger name is changed from `IdempotencyAttributeFilter` to `Idempotency`. Thus, in the `appsettings.json` file we should configure the logging level using the name `IdempotentAPI.Core.Idempotency` e.g., `"IdempotentAPI.Core.Idempotency": "Information"`.
- Dependencies of `IdempotentAPI`:
  - Remove `Microsoft.AspNetCore (2.2.0)`.
  - Remove `Microsoft.AspNetCore.Mvc.Abstractions (2.2.0)`.
  - Remove `Microsoft.Extensions.Caching.Abstractions (2.2.0)`.
  - Update `Newtonsoft.Json` from `12.0.2` to `12.0.3`.
### Fixed
- 🌟 Prevent concurrent requests in our default caching implementation (IdempotentAPI.Cache.DistributedCache).
### Thanks to
- @fjsosa for proposing a workaround to use `IdempotentAPI` in ASP.NET Core 5.0 and 6.0 in the meantime ([#17](https://github.com/ikyriak/IdempotentAPI/issues/17)) 🙏.
- @william-keller for reporting the [#23](https://github.com/ikyriak/IdempotentAPI/issues/23) and [#25](https://github.com/ikyriak/IdempotentAPI/issues/25) issues 🙏❤.



## [0.2.3-beta] - 2021-10-15
### Fixed
- Issue: An `Invalid character in chunk size error` occurs on the second request when using the Kestrel server ([#21](https://github.com/ikyriak/IdempotentAPI/issues/21)). For that purpose, we are not caching the `Transfer-Encoding` in the first request (excluded from cache).
- Thanks to @apchenjun and @william-keller for reporting and helping solve this issue 💪🙏.



## [0.2.1-beta] - 2021-07-12
### Added
- Handle inflight (concurrent and long-running) requests. In such cases, the subsequent exact requests will get a `409 Conflict` response.

### Fixed
- Issue: Duplication on concurrent requests with the same key ([#19](https://github.com/ikyriak/IdempotentAPI/issues/19)).
- Thanks to @lvzhuye and @RichardGreen-IS2 for reporting and [fixing](https://github.com/ikyriak/IdempotentAPI/pull/20) the issue 🙏❤.



## [0.2.0-beta] - 2021-06-19
### Added
- A sample project is added (using .NET Core 3.1).

### Fixed
- Issue: Accessing form data throws an exception when the Content-Type is not supported ([#14](https://github.com/ikyriak/IdempotentAPI/issues/14)).
- Thanks to @apchenjun for reporting the issue 🙏.



## [0.1.0-beta] - 2019-11-04
### Added
- Support idempotency by implementing an ASP.NET Core attribute (`[Idempotent()]`) by which any HTTP write operations (POST and PATCH) can have effect only once for the given request data.
- Use the `IDistributedCache` to cache the appropriate data. In this way, you can register your implementation, such as Memory Cache, SQL Server cache, Redis cache, etc.
- Set different options per controller or/and action method via the `Idempotent` attribute, such as:
  - `Enabled` (default: true): Enable or Disable the Idempotent operation on an API Controller's class or method.
  - `ExpireHours` (default: 24): The cached idempotent data retention period.
  - `HeaderKeyName` (default: `IdempotencyKey`): The name of the Idempotency-Key header.
  - `DistributedCacheKeysPrefix` (default: `IdempAPI_`): A prefix for the key names that we will use in the `DistributedCache`.
