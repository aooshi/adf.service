using System;
using System.Collections.Generic;
using System.Text;

namespace Adf.Service
{
    /// <summary>
    /// service state
    /// </summary>
    public enum ServiceState : int
    {
        /// <summary>
        /// Running 
        /// </summary>
        Initialize = 0,
        /// <summary>
        /// Running
        /// </summary>
        Running = 1,
        /// <summary>
        /// Master
        /// </summary>
        Master = 2,
        /// <summary>
        /// Slave
        /// </summary>
        Slave = 3,
        /// <summary>
        /// Witness
        /// </summary>
        Witness = 4,
        /// <summary>
        /// restore
        /// </summary>
        Restore = 5,
        /// <summary>
        /// Stoped
        /// </summary>
        Stoped = 6,
        ///// <summary>
        ///// Terminated
        ///// </summary>
        //Terminated = 7,
    }
}