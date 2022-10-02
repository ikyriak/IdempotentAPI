using Xunit;

namespace IdempotentAPI.IntegrationTests.Infrastructure
{
    [CollectionDefinition(nameof(CollectionFixture))]
    public class CollectionFixture : ICollectionFixture<Fixture>
    {
        // No code here! Its used to apply the [CollectionDefinition].
    }
}
