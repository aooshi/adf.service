using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.ServiceProcess;
using System.Text;

namespace Adf.Service
{
    internal class Service : System.ServiceProcess.ServiceBase
    {
        ServiceContext serviceContext;

        /// <summary> 
        /// 必需的设计器变量。
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        public Service(string serviceName)
        {
            components = new System.ComponentModel.Container();
            this.serviceContext = new ServiceContext(serviceName, false);
        }

        protected override void OnStart(string[] args)
        {
            this.serviceContext.Start(args);
        }

        protected override void OnStop()
        {
            this.serviceContext.Stop();
        }

        /// <summary>
        /// 清理所有正在使用的资源。
        /// </summary>
        /// <param name="disposing">如果应释放托管资源，为 true；否则为 false。</param>
        protected override void Dispose(bool disposing)
        {
            this.serviceContext.Destroy();

            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}