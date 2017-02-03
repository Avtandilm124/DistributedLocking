﻿using System;
using System.Data.SqlClient;
using System.Diagnostics;
using Gibraltar.DistributedLocking.Internal;

namespace Gibraltar.DistributedLocking
{
    /// <summary>
    /// Provide distributed locks via a SQL Server Database
    /// </summary>
    /// <remarks>Locks are done using SQL Server's lock manager and do not affect or require any schema.</remarks>
    [DebuggerDisplay("Database: {Name}")]
    public class SqlLockProvider : IDistributedLockProvider
    {
        private readonly string _connectionString;
        private readonly int _queryTimeout = 30;

        /// <summary>
        /// Create a new connection string to the database defining the scope of the lock
        /// </summary>
        public SqlLockProvider(string connectionString)
        {
            if (string.IsNullOrWhiteSpace(connectionString))
                throw new ArgumentNullException(nameof(connectionString));

            _connectionString = connectionString;

            //parse it so we can create a nice name and force an option..
            var connStringBuilder = new SqlConnectionStringBuilder(_connectionString);
            Name = string.Format("{0}:{1}", connStringBuilder.DataSource, connStringBuilder.InitialCatalog);
        }

        /// <inheritdoc />
        public string Name { get; }

        /// <inheritdoc />
        public IDisposable GetLock(string name)
        {
            SqlLock sqlLock = null;
            try
            {
                sqlLock = new SqlLock(_connectionString);

                var result = sqlLock.GetApplicationLock(name, SqlLockMode.Update, 0);
                if (result >= 0)
                    return sqlLock;

                sqlLock.SafeDispose();
            }
            catch (Exception)
            {
                sqlLock.SafeDispose();
            }

            return null;
        }

        /// <inheritdoc />
        public IDisposable GetLockRequest(string name)
        {
            SqlLock sqlLock = null;
            try
            {
                sqlLock = new SqlLock(_connectionString);

                var requestLockName = GetRequestLockName(name);

                var result = sqlLock.GetApplicationLock(requestLockName, SqlLockMode.Shared, 0);
                if (result >= 0)
                    return sqlLock;

                sqlLock.SafeDispose();
            }
            catch (Exception)
            {
                sqlLock.SafeDispose();
            }

            return null;
        }

        /// <inheritdoc />
        public bool CheckLockRequest(string name)
        {
            bool lockRequestPending = false;
            using (var sqlLock = new SqlLock(_connectionString))
            {
                try
                {
                    var requestLockName = GetRequestLockName(name);

                    //if there are no other threads trying to request a lock then we'll get an exclusive
                    //lock on it. Otherwise we won't :)
                    var result = sqlLock.PeekApplicationLock(requestLockName, SqlLockMode.Exclusive);
                    if (result < 0)
                    {
                        lockRequestPending = true;
                    }
                }
                catch (Exception)
                {
                    //we don't care why we failed, we presume that means there is no pending request.
                    
                }
            }

            return lockRequestPending;
        }

        private string GetRequestLockName(string lockName)
        {
            //we create a name that is very much unlikely to collide with a user intended name...
            return lockName + "~RequestToLock";
        }
    }
}
