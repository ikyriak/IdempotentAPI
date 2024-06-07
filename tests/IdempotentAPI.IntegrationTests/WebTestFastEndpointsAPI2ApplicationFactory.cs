extern alias TestFastEndpointsAPIs;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace IdempotentAPI.IntegrationTests
{
    public class WebTestFastEndpointsAPI2ApplicationFactory : WebApplicationFactory<TestFastEndpointsAPIs::Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.ConfigureAppConfiguration(c => c.AddInMemoryCollection(new Dictionary<string, string>()
            {
                {"Caching", "FusionCache"},
                {"DALock", "MadelsonDistLock"}
            }));
        }
    }
}