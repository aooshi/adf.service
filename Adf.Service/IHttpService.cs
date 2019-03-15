using System;
using System.Net;

namespace Adf.Service
{
    /// <summary>
    /// IService
    /// </summary>
    public interface IHttpService
    {
        /// <summary>
        /// http process function
        /// </summary>
        /// <param name="httpContext"></param>
        HttpStatusCode HttpProcess(HttpServerContext httpContext);
    }
}