using System;
using System.Collections.Generic;
using System.Text;

namespace Adf.Service
{
    /// <summary>
    /// service state change
    /// </summary>
    /// <param name="sc"></param>
    /// <param name="state"></param>
    public delegate void ServiceChange(ServiceContext sc, ServiceState state);
}
