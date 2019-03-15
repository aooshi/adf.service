using System;
using System.Collections.Generic;
using System.Text;
using System.Net;
using System.Configuration;
using System.Collections;

namespace Adf.Service
{
    /// <summary>
    /// Service Context
    /// </summary>
    public class ServiceContext
    {
        Smtp smtp;
        HAClient haClient;
        HAServer haServer;

        /// <summary>
        /// state change to master
        /// </summary>
        public event EventHandler ToMaster;

        /// <summary>
        /// state change to slave
        /// </summary>
        public event EventHandler ToSlave;

        /// <summary>
        /// state change to witness
        /// </summary>
        public event EventHandler ToWitness;

        /// <summary>
        /// state change to restore
        /// </summary>
        public event EventHandler ToRestore;

        /// <summary>
        /// get or set user-defined state value
        /// </summary>
        public object UserState
        {
            get;
            set;
        }

        string[] startArgs;
        /// <summary>
        /// get service start args
        /// </summary>
        public string[] StartArgs
        {
            get { return this.startArgs; }
        }

        DateTime startTime = DateTime.MinValue;
        /// <summary>
        /// get service start time
        /// </summary>
        public DateTime StartTime
        {
            get { return this.startTime; }
        }

        IService service;
        /// <summary>
        /// Service
        /// </summary>
        public IService Service
        {
            get { return this.service; }
        }

        bool isConsole;
        /// <summary>
        /// get a value indicates whether to console mode
        /// </summary>
        public bool IsConsole
        {
            get { return this.isConsole; }
        }

        LogManager logManager;
        /// <summary>
        /// get log manager
        /// </summary>
        public LogManager LogManager
        {
            get { return this.logManager; }
        }

        HttpServer httpServer;
        /// <summary>
        /// get http server object
        /// </summary>
        public HttpServer HttpServer
        {
            get { return this.httpServer; }
        }

        string serviceName;
        /// <summary>
        /// get service name
        /// </summary>
        public string ServiceName
        {
            get { return this.serviceName; }
        }

        ServiceState serviceState = ServiceState.Initialize;

        /// <summary>
        /// get service running state
        /// </summary>
        public ServiceState ServiceState
        {
            get { return this.serviceState; }
        }

        string[] mailRecipients = new string[0];
        /// <summary>
        /// Mail Recipient List, AppSetting["MailRecipients"]
        /// </summary>
        public string[] MailRecipients
        {
            get { return this.mailRecipients; }
        }

        /// <summary>
        /// get default smtp object
        /// </summary>
        public Smtp Smtp
        {
            get { return this.smtp; }
        }

        /// <summary>
        /// is enable master/slave mode
        /// </summary>
        public bool HAEnable
        {
            get { return HAContext.Enable; }
        }
        
        /// <summary>
        /// context
        /// </summary>
        /// <param name="isConsole"></param>
        /// <param name="serverName"></param>
        internal ServiceContext(string serverName, bool isConsole)
        {
            this.startTime = DateTime.Now;
            this.serviceName = serverName;
            this.isConsole = isConsole;
            this.serviceState = Adf.Service.ServiceState.Initialize;
            //
            this.InitilaizeLogManager();
            this.InitilaizeMailSender();
            this.InitilaizeService();
            this.InitilaizeHttpServer();
            this.InitializeHA();
        }

        /// <summary>
        /// change status to master
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="args"></param>
        private void ChangeToMaster(object sender, EventArgs args)
        {
            if (this.serviceState == Adf.Service.ServiceState.Master)
            {
                return;
            }

            this.logManager.Message.WriteTimeLine("Change to master");

            this.serviceState = Adf.Service.ServiceState.Master;

            var action = this.ToMaster;
            if (action != null)
            {
                action(this, EventArgs.Empty);
            }
        }

        /// <summary>
        /// change status to slave
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="args"></param>
        private void ChangeToSlave(object sender, EventArgs args)
        {
            if (this.serviceState == Adf.Service.ServiceState.Slave)
            {
                return;
            }

            this.logManager.Message.WriteTimeLine("Change to slave");

            this.serviceState = Adf.Service.ServiceState.Slave;

            var action = this.ToSlave;
            if (action != null)
            {
                action(this, EventArgs.Empty);
            }
        }

        /// <summary>
        /// change status to restore
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="args"></param>
        private void ChangeToRestore(object sender, EventArgs args)
        {
            if (this.serviceState == Adf.Service.ServiceState.Restore)
            {
                return;
            }

            this.logManager.Message.WriteTimeLine("Change to restore");

            this.serviceState = Adf.Service.ServiceState.Restore;

            var action = this.ToRestore;
            if (action != null)
            {
                action(this, EventArgs.Empty);
            }
        }

        /// <summary>
        /// change status to witness
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="args"></param>
        private void ChangeToWitness(object sender, EventArgs args)
        {
            if (this.serviceState == Adf.Service.ServiceState.Witness)
            {
                return;
            }

            this.logManager.Message.WriteTimeLine("Change to witness");

            this.serviceState = Adf.Service.ServiceState.Witness;

            var action = this.ToWitness;
            if (action != null)
            {
                action(this, EventArgs.Empty);
            }
        }

        private void InitializeHA()
        {
            HAContext.LoadConfiguration(this.logManager);
            if (HAContext.Enable)
            {
                HAContext.LoadIdentifier(this.logManager);
                HAContext.LoadMasterKey(this.logManager);
                //HAContext.LoadVip();
                HAContext.PrintLog(this.LogManager.Message);
            }
        }

        private void InitilaizeLogManager()
        {
            this.logManager = new LogManager(this.ServiceName);
            this.logManager.ToConsole = this.IsConsole;
        }

        private void InitilaizeMailSender()
        {
            this.smtp = new Smtp();
            //
            var cfgMailRecipients = ConfigHelper.GetSetting("MailRecipients", "");
            this.mailRecipients = cfgMailRecipients.Split(';');
            if (this.mailRecipients.Length > 0)
            {
                foreach (var item in this.mailRecipients)
                {
                    this.logManager.Message.WriteTimeLine("Mail Recipients: {0}", item);
                }
            }
        }

        private void InitilaizeService()
        {
            var serverType = System.Reflection.Assembly.GetEntryAssembly().EntryPoint.DeclaringType;
            this.service = Activator.CreateInstance(serverType) as IService;
            if (this.service == null)
            {
                throw new ServiceException(string.Concat("not support service type : ", serverType.Name));
            }
        }

        private void InitilaizeHttpServer()
        {
            if (this.service is IHttpService)
            {
                var portString = ConfigurationManager.AppSettings["HttpPort"];
                var ip = ConfigurationManager.AppSettings["HttpIp"] ?? "127.0.0.1";
                if (portString != null)
                {
                    var port = ConvertHelper.ToInt32(portString);
                    if (port == 0)
                        throw new ConfigurationErrorsException("configuration HttpPort invalid to appSetting");

                    if (string.IsNullOrEmpty(ip))
                        throw new ConfigurationErrorsException("configuration HttpIp invalid to appSetting");

                    this.httpServer = new HttpServer(port, ip);
                    this.httpServer.Callback = ((IHttpService)this.service).HttpProcess;
                    this.httpServer.Error += this.httpServerErrorCallback;
                    this.logManager.Message.WriteTimeLine("Http Address:" + ip);
                    this.logManager.Message.WriteTimeLine("Http Port:" + port);
                }
            }
        }

        private void httpServerErrorCallback(object sender, HttpServerErrorEventArgs e)
        {
            try
            {
                this.logManager.Error.WriteTimeLine(e.Exception.ToString());
            }
            catch { }
        }

        internal void Start(string[] startArgs)
        {
            this.startArgs = startArgs;
            if (this.httpServer != null)
            {
                this.httpServer.Start();
            }
            if (startArgs.Length > 0)
            {
                this.logManager.Message.WriteTimeLine("Start Args:" + string.Join(",", this.startArgs));
            }
            else
            {
                this.logManager.Message.WriteTimeLine("Start");
            }
            this.service.Start(this);
            this.logManager.Message.WriteTimeLine("Started");
            this.serviceState = Adf.Service.ServiceState.Running;
            //
            if (this.HAEnable == true)
            {
                this.haClient = new HAClient(this);
                this.haClient.ToMaster += this.ChangeToMaster;
                this.haClient.ToSlave += this.ChangeToSlave;
                this.haClient.ToWitness += this.ChangeToWitness;
                this.haClient.ToRestore += this.ChangeToRestore;
                this.haClient.Initialize();
                this.haServer = new HAServer(this);
            }
        }

        internal void Stop()
        {
            if (this.HAEnable == true)
            {
                this.haServer.Dispose();
                this.haClient.Dispose();
            }
            this.service.Stop(this);
            if (this.httpServer != null)
            {
                this.httpServer.Stop();
            }
            this.serviceState = Adf.Service.ServiceState.Stoped;
            this.logManager.Message.WriteTimeLine("Stoped");
            this.logManager.Flush();
        }

        /// <summary>
        /// get master host
        /// </summary>
        /// <returns>if no enable ha return null</returns>
        public string GetMaster()
        {
            if (this.HAEnable == false)
                return null;

            string host = null;

            var num = HAContext.MasterNum;
            if (num == 1)
                host = HAContext.Node1Address;
            else if (num == 2)
                host = HAContext.Node2Address;

            return host;
        }
        
        internal void Destroy()
        {
            if (this.httpServer != null)
            {
                this.httpServer.Dispose();
            }
            this.logManager.Flush();
            //
            this.ToSlave = null;
            this.ToMaster = null;
        }
        
    }
}