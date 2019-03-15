using System;

namespace Adf.Service
{
    /// <summary>
    /// IService
    /// </summary>
    public interface IService
    {
        /// <summary>
        /// service start
        /// </summary>
        /// <param name="serviceContext"></param>
        void Start(ServiceContext serviceContext);

        /// <summary>
        /// service stop
        /// </summary>
        /// <param name="serviceContext"></param>
        void Stop(ServiceContext serviceContext);
    }
}
