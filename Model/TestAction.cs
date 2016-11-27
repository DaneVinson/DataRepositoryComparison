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
        /// Get all things in bulk.
        /// </summary>
        GetAll,

        /// <summary>
        /// Get all things one at a time.
        /// </summary>
        Get,

        /// <summary>
        /// Delete all things one at a time.
        /// </summary>
        Delete
    }
}
