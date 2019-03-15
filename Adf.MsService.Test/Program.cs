using System;
using System.Collections.Generic;
using System.Text;

namespace Adf.MsService.Test
{
    public class Program : Adf.Service.IService, Adf.Service.IHAService
    {
        static void Main(string[] args)
        {
            Adf.Service.ServiceHelper.Entry(args);
        }

        public void Start(Service.ServiceContext serviceContext)
        {

        }

        public void Stop(Service.ServiceContext serviceContext)
        {
        }


        public void OnActive(Service.ServiceContext serviceContext)
        {
            throw new NotImplementedException();
        }

        public void OnStandby(Service.ServiceContext serviceContext)
        {
            throw new NotImplementedException();
        }

        public void OnWitness(Service.ServiceContext serviceContext)
        {
            throw new NotImplementedException();
        }
    }
}