namespace IdempotentAPI.Core;

public interface IKeyGenerator
{
    string Generate(string prefix, string controller, string action, string idempotencyKey);
}