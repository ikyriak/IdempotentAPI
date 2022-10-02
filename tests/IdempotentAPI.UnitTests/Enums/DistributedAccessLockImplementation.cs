namespace IdempotentAPI.UnitTests.Enums
{
    public enum DistributedAccessLockImplementation
    {
        None = 0,
        RedLockDotNet = 1,
        MadelsonDistributedLock = 2,
    }
}
