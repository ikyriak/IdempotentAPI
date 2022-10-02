namespace IdempotentAPI.TestWebAPIs1.Extensions
{
    public static class ConfigureApiVersioning
    {
        /// <summary>
        /// Configure versioning properties of the project, such as return headers, version format, etc.
        /// </summary>
        /// <param name="services"></param>
        public static void AddApiVersioningConfigured(this IServiceCollection services)
        {
            services.AddApiVersioning(options =>
            {
                options.ReportApiVersions = true;
            });

            services.AddVersionedApiExplorer(options =>
            {
                options.GroupNameFormat = "'v'VVV";
                options.SubstituteApiVersionInUrl = true;
            });
        }
    }
}
