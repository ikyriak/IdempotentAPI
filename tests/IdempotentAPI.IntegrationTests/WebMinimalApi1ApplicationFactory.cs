﻿using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace IdempotentAPI.IntegrationTests
{
    public class WebMinimalApi1ApplicationFactory : WebApplicationFactory<Program>
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