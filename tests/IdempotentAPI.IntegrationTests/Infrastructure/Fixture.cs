namespace IdempotentAPI.IntegrationTests.Infrastructure
{
    public class Fixture : IDisposable
    {
        public HttpClient Client1 { get; init; }
        public HttpClient Client2 { get; init; }

        public Fixture()
        {
            Client1 = new HttpClient()
            {
                BaseAddress = new Uri("http://localhost:5259/"),
            };

            Client2 = new HttpClient()
            {
                BaseAddress = new Uri("http://localhost:5260/"),
            };
        }

        public void Dispose()
        {
            Client1?.Dispose();
            Client2?.Dispose();
        }
    }
}