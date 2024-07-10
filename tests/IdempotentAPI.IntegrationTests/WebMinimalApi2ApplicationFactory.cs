extern alias TestWebMinimalAPIs;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace IdempotentAPI.IntegrationTests
{
    public class WebMinimalApi2ApplicationFactory : WebApplicationFactory<TestWebMinimalAPIs::Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.ConfigureAppConfiguration(c => c.AddInMemoryCollection(new Dictionary<string, string?>()
            {
                {"Caching", "FusionCache"},
                {"DALock", "MadelsonDistLock"}
            }));
        }
    }
}