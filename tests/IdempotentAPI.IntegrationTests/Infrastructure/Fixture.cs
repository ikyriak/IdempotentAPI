using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using StackExchange.Redis;

namespace IdempotentAPI.IntegrationTests.Infrastructure
{
    public class Fixture : IDisposable
    {
        public HttpClient Client1 { get; }
        public HttpClient Client2 { get; }
        public HttpClient Client3 { get; }

        public HttpClient TestServerClient1 { get; }
        public HttpClient TestServerClient2 { get; }
        public HttpClient TestServerClient3 { get; }
        
        public HttpClient TestServerClientRedis1 { get; }
        public HttpClient TestServerClientRedis2 { get; }
        
        public Fixture()
        {
            var defaultHttpClientHandler = new HttpClientHandler
            {
                // Disable usage of proxy
                Proxy = new WebProxy(HttpClient.DefaultProxy.GetProxy(new Uri("http://localhost")), true)
            };
            
            Client1 = new HttpClient(defaultHttpClientHandler)
            {
                BaseAddress = new Uri("http://localhost:5259/"),
            };

            Client2 = new HttpClient(defaultHttpClientHandler)
            {
                BaseAddress = new Uri("http://localhost:5260/"),
            };
            
            Client3 = new HttpClient(defaultHttpClientHandler)
            {
                BaseAddress = new Uri("http://localhost:5261/"),
            };
            
            var host1 = new WebApplicationFactory<TestWebAPIs1.Program>()
                .WithWebHostBuilder(builder =>
                    builder
                        .UseSetting("Caching", "MemoryCache")
                        .UseSetting("DALock", "None"));
            TestServerClient1 = host1.CreateClient();

            var host2 = new WebApplicationFactory<TestWebAPIs2.Program>()
                .WithWebHostBuilder(builder =>
                    builder
                        .UseSetting("Caching", "MemoryCache")
                        .UseSetting("DALock", "None"));
            TestServerClient2 = host2.CreateClient();

            var host3 = new WebApplicationFactory<Program>()
                .WithWebHostBuilder(builder =>
                    builder
                        .UseSetting("Caching", "MemoryCache")
                        .UseSetting("DALock", "None"));
            TestServerClient3 = host3.CreateClient();

            try
            {
                var hostRedis1 = new WebApplicationFactory<TestWebAPIs1.Program>()
                    .WithWebHostBuilder(builder =>
                        builder
                            .UseSetting("Caching", "FusionCache")
                            .UseSetting("DALock", "MadelsonDistLock"));
                TestServerClientRedis1 = hostRedis1.CreateClient();

                var hostRedis2 = new WebApplicationFactory<TestWebAPIs2.Program>()
                    .WithWebHostBuilder(builder =>
                        builder
                            .UseSetting("Caching", "FusionCache")
                            .UseSetting("DALock", "MadelsonDistLock"));
                TestServerClientRedis2 = hostRedis2.CreateClient();
            }
            catch (RedisConnectionException redisConnectionException)
            {
                Console.WriteLine(
                    $"Redis not running, test hosts not initialized, Exception: {redisConnectionException}");
            }
        }

        public void Dispose()
        {
            Client1.Dispose();
            Client2.Dispose();
            Client3.Dispose();
            
            TestServerClient1.Dispose();
            TestServerClient2.Dispose();
            TestServerClient3.Dispose();
        }
    }
}