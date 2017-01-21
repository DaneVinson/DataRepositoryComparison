using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Model
{
    public enum TestAction
    {
        /// <summary>
        /// Create things one at a time.
        /// </summary>
        Create = 1,

        /// <summary>
        /// Get all things.
        /// </summary>
        GetAll,

        /// <summary>
        /// Get things one at a time.
        /// </summary>
        Get,

        /// <summary>
        /// Delete things one at a time.
        /// </summary>
        Delete,

        /// <summary>
        /// Create things one at a time asynchronously across a collection.
        /// </summary>
        CreateAsync,

        /// <summary>
        /// Get all things asynchronously.
        /// </summary>
        GetAllAsync,

        /// <summary>
        /// Get things one at a time asynchronously across a collection of Id's.
        /// </summary>
        GetAsync,

        /// <summary>
        /// Delete all things one at a time asynchronously across a collection of Id's.
        /// </summary>
        DeleteAsync
    }
}
