using System.Net;

namespace IdempotentAPI.IntegrationTests.Infrastructure
{
    public class Fixture : IDisposable
    {
        public HttpClient Client1 { get; }
        public HttpClient Client2 { get; }
        public HttpClient Client3 { get; }

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
        }

        public void Dispose()
        {
            Client1.Dispose();
            Client2.Dispose();
            Client3.Dispose();
        }
    }
}