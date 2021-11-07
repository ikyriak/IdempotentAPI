using System;
using System.Collections.Generic;
using System.Threading;

namespace IdempotentAPI.Cache.DistributedCache.Lockers
{
    /// <summary>
    /// Value locker
    /// </summary>
    /// <example>
    /// string myValue = "valueToBeLocked"
    /// using (var locker = new ValueLocker(myValue)){
    ///     // ...
    /// }
    /// </example>
    internal sealed class ValueLocker : IDisposable
    {
        private static readonly object localLocker = new object();
        private static readonly Dictionary<string, LockDisposeTracker> lockValueMapper = new Dictionary<string, LockDisposeTracker>();

        private readonly string _lockedValue;

        public ValueLocker(string valueToLock)
        {
            _lockedValue = valueToLock;

            LockDisposeTracker disposeTracker;
            lock (localLocker)
            {
                if (!lockValueMapper.TryGetValue(valueToLock, out disposeTracker))
                {
                    disposeTracker = new LockDisposeTracker();
                    lockValueMapper.Add(valueToLock, disposeTracker);
                }
                disposeTracker.Count++;
            }
            Monitor.Enter(disposeTracker);
        }

        public void Dispose()
        {
            lock (localLocker)
            {
                LockDisposeTracker minlockValueMapperiLock = lockValueMapper[_lockedValue];
                minlockValueMapperiLock.Count--;
                if (minlockValueMapperiLock.Count == 0)
                    lockValueMapper.Remove(_lockedValue);

                Monitor.Exit(minlockValueMapperiLock);
            }
        }

        private sealed class LockDisposeTracker
        {
            public int Count;
        }
    }
}
