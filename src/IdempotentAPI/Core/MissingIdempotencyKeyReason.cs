namespace IdempotentAPI.Core;

public enum MissingIdempotencyKeyReason
{
    HeaderNotPresentInRequest,
    HeaderMissingValueInRequest,
    MultipleHeadersInReques
}