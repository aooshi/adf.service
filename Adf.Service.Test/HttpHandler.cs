using System;
using System.Collections.Generic;
using System.Text;

namespace Adf.Service.Test
{
   public class HttpHandler : Adf.IHttpServerHandler
    {
        public System.Net.HttpStatusCode Process(HttpServerContext httpContext)
        {
            httpContext.Content = "1";
            return System.Net.HttpStatusCode.OK;
        }
    }
}
