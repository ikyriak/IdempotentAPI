namespace IdempotentAPI.Core;

public class DefaultKeyGenerator : IKeyGenerator
{
    public string Generate(string prefix, string controller, string action, string idempotencyKey)
    {
        return $"{prefix}{idempotencyKey}";
    }
}