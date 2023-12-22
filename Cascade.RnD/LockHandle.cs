using System;
using System.Threading;

namespace Cascade.RnD {
	public class LockHandle : IDisposable
	{
		private readonly ReaderWriterLockSlim _lock;
		private readonly bool _isWriteLock;

		public LockHandle(ReaderWriterLockSlim rwLock, bool isWriteLock)
		{
			_lock = rwLock;
			_isWriteLock = isWriteLock;

			if (_isWriteLock)
				_lock.EnterWriteLock();
			else
				_lock.EnterReadLock();
		}

		public void Dispose()
		{
			if (_isWriteLock)
				_lock.ExitWriteLock();
			else
				_lock.ExitReadLock();
		}
	}
}