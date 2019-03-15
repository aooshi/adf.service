using System;
using System.Threading;
using System.Net;

namespace Adf.Service
{
    class HAClient : IDisposable
    {
        private ServiceContext serviceContext;
        private LogManager logManager;
        private bool disposed = false;
        //
        private WebSocketClient[] clients;
        //
        private int elect1 = 0;
        private int elect2 = 0;
        private int elect3 = 0;
        private string electIdentity = "";
        private int electWaitTimeout = 5000;
        private EventWaitHandle electWaitHandle;
        private EventWaitHandle electEndWaitHandle;
        private Thread electThread;
        private Object electLock = new object();
        //
        private QueueTask<string> taskQueue;
        //
        private Thread[] keepaliveThreads;
        private EventWaitHandle[] keepaliveWaitHandles;
        private EventWaitHandle keepaliveEndWaitHandle;
        //
        private bool readMasterKey = false;


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


        public HAClient(ServiceContext serviceContext)
        {
            this.serviceContext = serviceContext;
            this.logManager = serviceContext.LogManager;
        }

        public void Initialize()
        {
            this.clients = new WebSocketClient[3];
            this.keepaliveWaitHandles = new EventWaitHandle[3];
            //
            this.taskQueue = new QueueTask<string>(this.TaskProcessor);
            //
            this.electWaitTimeout = HAContext.ElectTimeout;
            this.electWaitHandle = new EventWaitHandle(true, EventResetMode.ManualReset);
            this.electEndWaitHandle = new EventWaitHandle(false, EventResetMode.ManualReset);
            this.electThread = new Thread(this.ElectProcess);
            this.electThread.IsBackground = true;
            this.electThread.Start();
            //
            this.keepaliveThreads = new Thread[3];
            if (HAContext.Node1 != null)
            {
                this.InitialClient(HAContext.Node1, 1);
            }
            if (HAContext.Node2 != null)
            {
                this.InitialClient(HAContext.Node2, 2);
            }
            if (HAContext.Node3 != null)
            {
                this.InitialClient(HAContext.Node3, 3);
            }
            this.keepaliveEndWaitHandle = new EventWaitHandle(false, EventResetMode.ManualReset);
            //
            this.InitialWitness();
        }

        private void InitialWitness()
        {
            //选举时，证见服务器评分永不可能成为主/辅，因此排名第三位的设备直接确定为证见服务器
            if (HAContext.SelfNum == 3)
            {
                this.ChangeToWitness();
            }
        }

        private void InitialClient(IPEndPoint ep, int num)
        {
            var index = num - 1;
            this.clients[index] = new WebSocketClient(ep.Address.ToString(), ep.Port, "/ha", 0);
            this.clients[index].Connectioned += new EventHandler(ClientOnConnectioned);
            this.clients[index].Closed += new EventHandler<WebSocketCloseEventArgs>(ClientOnClosed);
            this.clients[index].Message += new EventHandler<WebSocketMessageEventArgs>(ClientOnMessage);
            this.clients[index].UserState = num;
            //
            this.keepaliveWaitHandles[index] = new EventWaitHandle(false, EventResetMode.AutoReset);
            //
            this.keepaliveThreads[index] = new Thread(this.Keepalive);
            this.keepaliveThreads[index].IsBackground = true;
            this.keepaliveThreads[index].Start(index);
        }

        private void Keepalive(object userState)
        {
            var index = (int)userState;
            var num = index + 1;
            var client = this.clients[index];
            //
            while (this.disposed == false)
            {
                this.keepaliveWaitHandles[index].WaitOne(HAContext.Keepalive);
                if (this.disposed == false)
                {
                    try
                    {
                        if (client.IsConnectioned == false)
                        {
                            client.Connection();
                        }
                        else
                        {
                            client.Ping();
                        }

                        //
                        if (HAContext.StoreEnable == true)
                        {
                            if (this.serviceContext.ServiceState == ServiceState.Slave)
                            {
                                if (this.readMasterKey == true && num == HAContext.MasterNum)
                                {
                                    client.SendAsync("readmasterkey", null);
                                }
                            }
                            else if (this.serviceContext.ServiceState == ServiceState.Witness)
                            {
                                if (this.readMasterKey == true && num == HAContext.MasterNum)
                                {
                                    client.SendAsync("readmasterkey", null);
                                }
                            }
                        }
                    }
                    catch
                    {
                    }
                }
            }
            //
            this.keepaliveEndWaitHandle.Set();
        }

        private void ClientOnMessage(object sender, WebSocketMessageEventArgs e)
        {
            var client = (WebSocketClient)sender;
            var num = (int)client.UserState;

            //
            if (e.Opcode == WebSocketOpcode.Text)
            {
                var messageItems = e.Message.Split(':');
                if (messageItems[0] == "elect")
                {
                    //req ,    elect:xxxxx:masterkey
                    //resp,    elect:xxxxx:num
                    var electNum = 0;
                    if (messageItems.Length == 3 && messageItems[1] == this.electIdentity && int.TryParse(messageItems[2], out electNum))
                    {
                        lock (this.electLock)
                        {
                            if (electNum == 1)
                            {
                                this.logManager.Message.WriteTimeLine("HAClient request ack node 1 from node {0}", client.UserState);
                                this.elect1++;
                            }
                            else if (electNum == 2)
                            {
                                this.logManager.Message.WriteTimeLine("HAClient request ack node 2 from node {0}", client.UserState);
                                this.elect2++;
                            }
                            else if (electNum == 3)
                            {
                                this.logManager.Message.WriteTimeLine("HAClient request ack node 3 from node {0}", client.UserState);
                                this.elect3++;
                            }
                            //
                            var count = 0;
                            count += HAContext.Connectioned1 ? 1 : 0;
                            count += HAContext.Connectioned2 ? 1 : 0;
                            count += HAContext.Connectioned3 ? 1 : 0;
                            //, ignore num 3
                            if (this.elect1 == 2)
                            {
                                this.taskQueue.Add("elected:1");
                            }
                            else if (this.elect2 == 2)
                            {
                                this.taskQueue.Add("elected:2");
                            }
                            else if (this.elect3 == 2)
                            {
                                this.taskQueue.Add("elected:3");
                            }
                        }
                    }
                }
                else if (messageItems[0] == "masterkey" && messageItems.Length == 2)
                {
                    HAContext.SaveMasterKey(messageItems[1]);
                    this.logManager.Message.WriteTimeLine("HAClient receive masterkey " + messageItems[1] + " from node " + num);
                    this.readMasterKey = false;
                }
            }
        }

        private void ClientOnClosed(object sender, WebSocketCloseEventArgs e)
        {
            var client = (WebSocketClient)sender;
            var num = (int)client.UserState;
            //
            if (num == 1)
            {
                HAContext.Connectioned1 = false;
            }
            else if (num == 2)
            {
                HAContext.Connectioned2 = false;
            }
            else if (num == 3)
            {
                HAContext.Connectioned3 = false;
            }
            //
            if (this.disposed == false)
            {
                var usable = 0;
                usable += HAContext.Connectioned1 == true ? 1 : 0;
                usable += HAContext.Connectioned2 == true ? 1 : 0;
                usable += HAContext.Connectioned3 == true ? 1 : 0;

                //node < 2  ,  to restore
                if (usable < 2)
                {
                    if (this.serviceContext.ServiceState == ServiceState.Master || this.serviceContext.ServiceState == ServiceState.Slave)
                    {
                        this.logManager.Message.WriteTimeLine("HAClient available nodes are less than 2 to restore.");
                        this.taskQueue.Add("restore");
                    }
                }

                this.taskQueue.Add("closed:" + num);
                
                this.logManager.Message.WriteTimeLine("HAClient node off-line for node " + num);

                //
                this.electWaitTimeout = HAContext.ElectTimeout;
                this.electWaitHandle.Set();
            }
        }

        private void ClientOnConnectioned(object sender, EventArgs e)
        {
            var client = (WebSocketClient)sender;
            var num = (int)client.UserState;
            //
            if (num == 1)
            {
                HAContext.Connectioned1 = true;
            }
            else if (num == 2)
            {
                HAContext.Connectioned2 = true;
            }
            else if (num == 3)
            {
                HAContext.Connectioned3 = true;
            }
            //
            if (this.disposed == false)
            {
                this.electWaitTimeout = HAContext.ElectTimeout;
                this.electWaitHandle.Set();
            }
        }

        private void ElectProcess()
        {
            while (this.disposed == false)
            {
                this.electWaitHandle.WaitOne(this.electWaitTimeout);
                //
                if (this.disposed == false)
                {
                    if (this.electWaitTimeout == HAContext.ElectTimeout)
                    {
                        this.elect1 = 0;
                        this.elect2 = 0;
                        this.elect3 = 0;
                        this.electIdentity = Guid.NewGuid().ToString("N");
                        //request
                        for (int i = 0; i < 3; i++)
                        {
                            if (this.clients[i] != null)
                            {
                                this.logManager.Message.WriteTimeLine("HAClient request elect by node " + (i + 1));
                                this.RequestElect(this.clients[i], this.electIdentity);
                            }
                        }
                    }
                    //
                    this.electWaitHandle.Reset();
                }
            }

            //
            this.electEndWaitHandle.Set();
        }

        private void RequestElect(WebSocketClient client, string identity)
        {
            try
            {
                //req ,    elect:xxxxx:masterkey
                //resp,    elect:xxxxx:num
                client.SendAsync("elect:" + identity + ":" + HAContext.MasterKey, null);
            }
            catch
            {
            }
        }

        private void TaskProcessor(string command)
        {
            var items = command.Split(':');
            var num = 0;
            if (items[0] == "restore")
            {
                //stop current elect , wait next elect
                this.electWaitTimeout = System.Threading.Timeout.Infinite;
                //
                this.ChangeToRestore();
            }
            else if (items.Length == 2)
            {
                if (items[0] == "elected" && int.TryParse(items[1], out num))
                {
                    //stop current elect , wait next elect
                    this.electWaitTimeout = System.Threading.Timeout.Infinite;
                    //
                    this.logManager.Message.WriteTimeLine("HA elected hit node {0}", num);
                    this.Elected(num);
                }
                else if (items[0] == "closed" && int.TryParse(items[1], out num))
                {
                    if (this.serviceContext.Smtp.Enabled == true && HAContext.Mails.Length > 0)
                    {
                        var host = "unknown host";
                        if (num == 1)
                            host = HAContext.Node1Address;
                        else if (num == 2)
                            host = HAContext.Node2Address;
                        else if (num == 3)
                            host = HAContext.Node3Address;

                        var mailAddress = new string[HAContext.Mails.Length];
                        var mailBody = this.serviceContext.ServiceName + " node off-line for " + host;
                        var mailHtml = false;
                        var mailSubject = this.serviceContext.ServiceName + " node off-line notify";

                        //
                        Array.Copy(HAContext.Mails, mailAddress, mailAddress.Length);

                        //
                        try
                        {
                            this.serviceContext.Smtp.Send(mailSubject
                                , mailBody
                                , mailHtml
                                , mailAddress);
                        }
                        catch (Exception exception)
                        {
                            this.logManager.Warning.WriteTimeLine("node off-line mail notify failure, " + exception.Message);
                        }
                    }
                }
            }
        }

        private void Elected(int num)
        {
            var selfIsMaster = false;
            //
            if (HAContext.SelfNum == 1 && num == 1)
            {
                selfIsMaster = true;
            }
            else if (HAContext.SelfNum == 2 && num == 2)
            {
                selfIsMaster = true;
            }
            //
            HAContext.MasterNum = num;
            //
            if (selfIsMaster == true && this.serviceContext.ServiceState == ServiceState.Master)
            {
                //keep & exit
            }
            else if (selfIsMaster == true)
            {
                this.ChangeToMaster();
            }
            else if (HAContext.SelfNum == 3)
            {
                //ignore
            }
            else
            {
                this.ChangeToSlave();
            }

            //
            this.readMasterKey = true;
        }

        private void ChangeToWitness()
        {
            var action = this.ToWitness;
            if (action != null)
            {
                action(this, EventArgs.Empty);
            }
        }

        private void ChangeToMaster()
        {
            var action = this.ToMaster;
            if (action != null)
            {
                action(this, EventArgs.Empty);
            }

            //broadcast master key
            //if (HAContext.StoreEnable)
            //{
            //    HAContext.MasterKey = Guid.NewGuid().ToString("N");
            //    HAContext.SaveMasterKey(HAContext.MasterKey);
            //}

            var mk = Guid.NewGuid().ToString("N");
            HAContext.SaveMasterKey(mk);
        }

        private void ChangeToSlave()
        {
            var action = this.ToSlave;
            if (action != null)
            {
                action(this, EventArgs.Empty);
            }

            this.readMasterKey = true;
        }

        private void ChangeToRestore()
        {
            HAContext.MasterNum = 0;
            //
            var action = this.ToRestore;
            if (action != null)
            {
                action(this, EventArgs.Empty);
            }
        }

        public void Dispose()
        {
            this.disposed = true;
            //
            this.electWaitHandle.Set();
            this.electEndWaitHandle.WaitOne();
            this.electWaitHandle.Close();
            //
            for (int i = 0; i < 3; i++)
            {
                if (this.clients[i] != null)
                {
                    this.clients[i].Dispose();
                }
            }
            //
            for (int i = 0; i < 3; i++)
            {
                if (this.keepaliveWaitHandles[i] != null)
                {
                    this.keepaliveWaitHandles[i].Set();
                }
            }
            this.keepaliveEndWaitHandle.WaitOne();
            for (int i = 0; i < 3; i++)
            {
                if (this.keepaliveWaitHandles[i] != null)
                {
                    this.keepaliveWaitHandles[i].Close();
                }
            }
            //
            this.taskQueue.Dispose();
            //
            this.ToWitness = null;
            this.ToSlave = null;
            this.ToRestore = null;
            this.ToMaster = null;
        }
    }
}
