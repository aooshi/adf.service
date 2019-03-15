using System;
using System.Collections.Generic;
using System.Text;

namespace Adf.Service
{
    /// <summary>
    /// Service Exception
    /// </summary>
    public class ServiceException : Exception
    {
        /// <summary>
        /// initialize new instance
        /// </summary>
        /// <param name="message"></param>
        public ServiceException(string message)
            : base(message)
        {
        }
    }
}