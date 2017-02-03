﻿#region File Header and License
// /*
//    When_Using_Local_File_Locks.cs
//    Copyright 2008-2017 Gibraltar Software, Inc.
//    
//    Licensed under the Apache License, Version 2.0 (the "License");
//    you may not use this file except in compliance with the License.
//    You may obtain a copy of the License at
// 
//        http://www.apache.org/licenses/LICENSE-2.0
// 
//    Unless required by applicable law or agreed to in writing, software
//    distributed under the License is distributed on an "AS IS" BASIS,
//    WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//    See the License for the specific language governing permissions and
//    limitations under the License.
// */
#endregion

using System;
using System.Diagnostics;
using System.IO;
using NUnit.Framework;

namespace Gibraltar.DistributedLocking.Test
{
    [TestFixture]
    public class When_Using_Local_File_Locks
    {
        const string MultiprocessLockName = "LockRepository";

        [Test]
        public void Can_Acquire_Lock()
        {
            var lockScopePath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            var lockManager = new DistributedLockManager(new FileLockProvider(lockScopePath));

            try
            {
                using (var outerLock = lockManager.Lock(this, MultiprocessLockName, 0))
                {
                    Assert.IsNotNull(outerLock, "Unable to acquire the lock");
                }
            }
            finally
            {
                Directory.Delete(lockScopePath);
            }
        }

        [Test]
        public void Can_Not_Aquire_Same_Lock_On_Another_Thread()
        {
            var lockScopePath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            var lockManager = new DistributedLockManager(new FileLockProvider(lockScopePath));

            try
            {
                using (var outerLock = lockManager.Lock(this, MultiprocessLockName, 0))
                {
                    using (var otherLock = OtherThreadLockHelper.TryLock(this, lockManager, MultiprocessLockName, 0))
                    {
                        Assert.IsNull(otherLock, "Another thread was allowed to get the lock");
                    }
                }
            }
            finally
            {
                Directory.Delete(lockScopePath);
            }
        }

        [Test]
        public void Can_ReEnter_Lock_On_Same_Thread()
        {
            var lockScopePath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            var lockManager = new DistributedLockManager(new FileLockProvider(lockScopePath));

            try
            {
                // First test new re-entrant lock capability.
                using (var outerLock = lockManager.Lock(this, MultiprocessLockName, 0))
                {
                    Assert.IsNotNull(outerLock, "Unable to outer lock the repository");

                    // Now check that we can get the same lock on the same thread.
                    using (var middleLock = lockManager.Lock(this, MultiprocessLockName, 0))
                    {
                        Assert.IsNotNull(middleLock, "Unable to reenter the repository lock on the same thread");

                        using (var innerLock = lockManager.Lock(this, MultiprocessLockName, 0))
                        {
                            Assert.IsNotNull(innerLock, "Unable to reenter the repository lock on the same thread twice");
                        }
                    }
                }
            }
            finally
            {
                Directory.Delete(lockScopePath);
            }
        }

        [Test]
        public void Can_Not_Acquire_Same_Lock_In_Same_Scope()
        {
            var lockScopePath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            var lockManager = new DistributedLockManager(new FileLockProvider(lockScopePath));

            try
            {
                // Now test other scenarios while another thread holds the lock.
                using (var testLock = OtherThreadLockHelper.TryLock(this, lockManager, MultiprocessLockName, 0))
                {
                    Assert.IsNotNull(testLock, "Unable to lock the repository");

                    //now that I have the test lock, it should fail if I try to get it again.
                    Assert.Catch<LockTimeoutException>(() =>
                    {
                        using (var failedLock = lockManager.Lock(this, MultiprocessLockName, 0))
                        {
                            Assert.IsNull(failedLock, "Duplicate lock was allowed.");
                        }
                    });
                }
            }
            finally
            {
                Directory.Delete(lockScopePath);
            }
        }

        [Test]
        public void Can_Acquire_Different_Lock_In_Same_Scope()
        {
            var lockScopePath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            var lockManager = new DistributedLockManager(new FileLockProvider(lockScopePath));

            try
            {
                // Now test other scenarios while another thread holds the lock.
                using (var otherLock = OtherThreadLockHelper.TryLock(this, lockManager, MultiprocessLockName + "_alternate", 0))
                {
                    Assert.IsNotNull(otherLock, "Unable to establish first lock in scope.");

                    using (var testLock = lockManager.Lock(this, MultiprocessLockName, 0))
                    {
                        Assert.IsNotNull(testLock, "Unable to establish second lock in scope.");
                    }
                }
            }
            finally
            {
                Directory.Delete(lockScopePath);
            }
        }

        [Test]
        public void Can_Acquire_Same_Lock_In_Different_Scope()
        {
            var firstTestRepositoryPath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            var secondTestRepositoryPath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            var thirdTestRepositoryPath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            var fourthTestRepositoryPath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());

            var firstLockManager = new DistributedLockManager(new FileLockProvider(firstTestRepositoryPath));

            try
            {
                // Now test other scenarios while another thread holds the lock.
                using (var testLock = firstLockManager.Lock(this, MultiprocessLockName, 0))
                {
                    Assert.IsNotNull(testLock, "Unable to establish lock on first scope");

                    var secondLockManager = new DistributedLockManager(new FileLockProvider(secondTestRepositoryPath));
                    using (var secondTestLock = secondLockManager.Lock(this, MultiprocessLockName, 0))
                    {
                        Assert.IsNotNull(secondTestLock, "Unable to establish lock on second scope.");

                        var thirdLockManager = new DistributedLockManager(new FileLockProvider(thirdTestRepositoryPath));
                        using (var thirdTestLock = thirdLockManager.Lock(this, MultiprocessLockName, 0))
                        {
                            Assert.IsNotNull(thirdTestLock, "Unable to establish lock on third scope.");

                            var forthLockManager = new DistributedLockManager(new FileLockProvider(fourthTestRepositoryPath));
                            using (var fourthTestLock = forthLockManager.Lock(this, MultiprocessLockName, 0))
                            {
                                Assert.IsNotNull(fourthTestLock, "Unable to establish lock on fourth scope.");
                            }
                        }
                    }
                }
            }
            finally
            {
                //and clean up after ourselves.
                if (Directory.Exists(firstTestRepositoryPath))
                    Directory.Delete(firstTestRepositoryPath, true);

                if (Directory.Exists(secondTestRepositoryPath))
                    Directory.Delete(secondTestRepositoryPath, true);

                if (Directory.Exists(thirdTestRepositoryPath))
                    Directory.Delete(thirdTestRepositoryPath, true);

                if (Directory.Exists(fourthTestRepositoryPath))
                    Directory.Delete(fourthTestRepositoryPath, true);
            }
        }

        [Test]
        public void LockRepositoryTimeout()
        {
            const string MultiprocessLockName = "LockRepositoryTimeout";

            var lockScopePath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());

            try
            {
                var lockManager = new DistributedLockManager(new FileLockProvider(lockScopePath));

                using (var testLock = OtherThreadLockHelper.TryLock(this, lockManager, MultiprocessLockName, 0))
                {
                    Assert.IsNotNull(testLock, "Unable to lock the repository");

                    //now when we try to get it we should not, and should wait at least our timeout
                    var lockStart = DateTimeOffset.Now;
                    DistributedLock timeoutLock;
                    Assert.IsFalse(lockManager.TryLock(this, MultiprocessLockName, 5, out timeoutLock));
                    using (timeoutLock)
                    {
                        //we shouldn't have the lock
                        Assert.IsNull(timeoutLock, "Duplicate lock allowed");

                        //and we should be within a reasonable delta of our timeout.
                        var delay = DateTimeOffset.Now - lockStart;
                        Trace.Write(string.Format("Repository Timeout Requested: {0} Actual: {1}", 5, delay.TotalSeconds));
                        Assert.Greater(delay.TotalSeconds, 4.5, "Timeout happened too fast - {0} seconds", delay.TotalSeconds);
                        Assert.Less(delay.TotalSeconds, 5.5, "Timeout happened too slow - {0} seconds", delay.TotalSeconds);
                    }
                }
            }
            finally
            {
                if (Directory.Exists(lockScopePath))
                    Directory.Delete(lockScopePath, true);
            }
        }

        [Test]
        public void Can_Acquire_Lock_Many_Times()
        {
            var lockScopePath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            var lockManager = new DistributedLockManager(new FileLockProvider(lockScopePath));

            var lockIterations = 1000;
            try
            {
                for (var curIteration = 0; curIteration < lockIterations; curIteration++)
                {
                    using (var outerLock = lockManager.Lock(this, MultiprocessLockName, 0))
                    {
                        Assert.IsNotNull(outerLock, "Unable to acquire lock");
                    }
                }
            }
            finally
            {
                Directory.Delete(lockScopePath);
            }
        }
    }
}
