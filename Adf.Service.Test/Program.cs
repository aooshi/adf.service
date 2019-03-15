using System;
using System.Collections.Specialized;
using System.Net;

namespace Adf.Service.Test
{
    /// <summary>
    /// 程序入口
    /// </summary>
    public class Program : IService
    {
        /// <summary>
        /// 应用程序的主入口点。
        /// </summary>
        /// <param name="args"></param>
        static void Main(string[] args)
        {
            Adf.Service.ServiceHelper.Entry(args);
        }
        
        public void Start(ServiceContext context)
        {
            context.LogManager.Message.WriteLine("My Service Start");
        }

        public void Stop(ServiceContext context)
        {
            context.LogManager.Message.WriteLine("My Service Stop");
        }
    }
}
