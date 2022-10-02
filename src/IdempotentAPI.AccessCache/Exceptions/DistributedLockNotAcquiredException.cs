using System;

namespace IdempotentAPI.AccessCache.Exceptions
{
    public class DistributedLockNotAcquiredException : Exception
    {

        public DistributedLockNotAcquiredException(string message) : base(message)
        {
        }

        public DistributedLockNotAcquiredException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }
}
