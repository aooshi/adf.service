using System;
using System.Net;

namespace Adf.Service
{
    class HAContext
    {
        public static bool Connectioned1 = false;
        public static bool Connectioned2 = false;
        public static bool Connectioned3 = false;
        //
        public static bool Enable = false;
        //
        public static string SelfAddress;
        public static int SelfNum = 0;
        public static int MasterNum = 0;
        public static string MasterKey = "9cbcb4916b01430dbd42c099999a6174";
        //
        public static int Keepalive = 0;
        public static int ElectTimeout = 0;
        //
        public static IPEndPoint Node1;
        public static IPEndPoint Node2;
        public static IPEndPoint Node3;

        public static string Node1Point;
        public static string Node2Point;
        public static string Node3Point;

        public static string Node1Address;
        public static string Node2Address;
        public static string Node3Address;

        private static string Path;
        public static bool StoreEnable = false;

        public static string[] Mails = new string[0];

        ////ip mask gateway
        //public static string Vip;
        //public static string VipEth;
        //public static bool VipEnable = false;

        public static void LoadConfiguration(LogManager logManager)
        {
            HAContext.Path = Adf.ConfigHelper.GetSetting("HA:Path", "").Trim();
            HAContext.StoreEnable = string.IsNullOrEmpty(HAContext.Path) == false;
            //
            var node1 = Adf.ConfigHelper.GetSetting("HA:Node1", "").Trim();
            var node2 = Adf.ConfigHelper.GetSetting("HA:Node2", "").Trim();
            var node3 = Adf.ConfigHelper.GetSetting("HA:Node3", "").Trim();
            //
            HAContext.Node1 = Adf.IpHelper.ParseEndPoint(node1);
            HAContext.Node2 = Adf.IpHelper.ParseEndPoint(node2);
            HAContext.Node3 = Adf.IpHelper.ParseEndPoint(node3);
            //
            HAContext.Node1Point = HAContext.Node1 == null ? "" : HAContext.Node1.ToString();
            HAContext.Node2Point = HAContext.Node2 == null ? "" : HAContext.Node2.ToString();
            HAContext.Node3Point = HAContext.Node3 == null ? "" : HAContext.Node3.ToString();
            //
            HAContext.Node1Address = HAContext.Node1 == null ? "" : HAContext.Node1.Address.ToString();
            HAContext.Node2Address = HAContext.Node2 == null ? "" : HAContext.Node2.Address.ToString();
            HAContext.Node3Address = HAContext.Node3 == null ? "" : HAContext.Node3.Address.ToString();
            //
            var keepalive = Adf.ConfigHelper.GetSettingAsInt("HA:Keepalive", 5) * 1000;
            if (keepalive < 5000 || keepalive > 60 * 1000)
            {
                logManager.Warning.WriteTimeLine("HA:Keepalive configuration invalid,value allow 5-60.");
                logManager.Flush();
                throw new ConfigException("HA:Keepalive configuration invalid,value allow 5-60.");
            }

            var electTimeout = Adf.ConfigHelper.GetSettingAsInt("HA:ElectTimeout", 5) * 1000;
            if (electTimeout < 5000 || electTimeout > 30 * 1000)
            {
                logManager.Warning.WriteTimeLine("HA:ElectTimeout configuration invalid, value allow 5-30.");
                logManager.Flush();
                throw new ConfigException("HA:ElectTimeout configuration invalid, value allow 5-30.");
            }
            //
            HAContext.Keepalive = keepalive;
            HAContext.ElectTimeout = electTimeout;
            //
            var count = 0;
            count += HAContext.Node1 != null ? 1 : 0;
            count += HAContext.Node2 != null ? 1 : 0;
            count += HAContext.Node3 != null ? 1 : 0;
            //
            HAContext.Enable = count > 1;
        }

        public static void LoadMails(LogManager logManager)
        {
            if (HAContext.Enable)
            {
                var mails = Adf.ConfigHelper.GetSetting("HA:Mails", "").Trim().Split(';');
                if (mails.Length > 0 && mails[0] != "")
                {
                    HAContext.Mails = mails;
                }
            }
        }

        public static void LoadIdentifier(LogManager logManager)
        {
            var hostname = System.Net.Dns.GetHostName();
            var address = System.Net.Dns.GetHostAddresses(hostname);
            //
            var match1 = Array.Exists<System.Net.IPAddress>(address, item =>
            {
                return HAContext.Node1 != null && HAContext.Node1.Address.Equals(item);
            });
            var match2 = Array.Exists<System.Net.IPAddress>(address, item =>
            {
                return HAContext.Node2 != null && HAContext.Node2.Address.Equals(item);
            });
            var match3 = Array.Exists<System.Net.IPAddress>(address, item =>
            {
                return HAContext.Node3 != null && HAContext.Node3.Address.Equals(item);
            });

            if (match1)
            {
                HAContext.SelfNum = 1;
                HAContext.SelfAddress = HAContext.Node1Address;
            }
            else if (match2)
            {
                HAContext.SelfNum = 2;
                HAContext.SelfAddress = HAContext.Node2Address;
            }
            else if (match3)
            {
                HAContext.SelfNum = 3;
                HAContext.SelfAddress = HAContext.Node3Address;
            }
            else
            {
                logManager.Warning.WriteTimeLine("ha invalid configuration node1/node2/node3 or not the host application");
                logManager.Warning.WriteTimeLine("the host ip address " + Adf.ConvertHelper.ArrayToString<System.Net.IPAddress>(address, ",", addr =>
                {
                    return addr.ToString();

                }) + " ).");
                logManager.Flush();

                throw new ConfigException("ha invalid configuration node1/node2/node3 or not the host application.");
            }
        }

        /// <summary>
        /// read master key from file
        /// </summary>
        /// <param name="logManager"></param>
        public static void LoadMasterKey(LogManager logManager)
        {
            //var path = HAContext.Path;
            //if (string.IsNullOrEmpty(path) == false)
            //{
            //    var filepath = System.IO.Path.Combine(path, "master.key");
            //    if (System.IO.File.Exists(filepath) == true)
            //    {
            //        var masterkey = System.IO.File.ReadAllText(filepath);
            //        HAContext.MasterKey = masterkey;
            //    }

            //    HAContext.StoreEnable = true;
            //}

            if (HAContext.StoreEnable == true)
            {
                var filepath = System.IO.Path.Combine(HAContext.Path, "master.key");
                if (System.IO.File.Exists(filepath) == true)
                {
                    var masterkey = System.IO.File.ReadAllText(filepath);
                    HAContext.MasterKey = masterkey;
                }
            }
        }

        /// <summary>
        /// save master key to file
        /// </summary>
        /// <param name="masterKey"></param>
        public static void SaveMasterKey(string masterKey)
        {
            //var path = HAContext.Path;
            //if (string.IsNullOrEmpty(path) == false)
            //{
            //    if (System.IO.Directory.Exists(path) == false)
            //    {
            //        System.IO.Directory.CreateDirectory(path);
            //    }
            //    var filepath = System.IO.Path.Combine(path, "master.key");
            //    System.IO.File.WriteAllText(filepath, masterKey);
            //    //
            //    HAContext.MasterKey = masterKey;
            //}

            if (HAContext.StoreEnable == true)
            {
                var path = HAContext.Path;
                if (System.IO.Directory.Exists(path) == false)
                {
                    System.IO.Directory.CreateDirectory(path);
                }
                var filepath = System.IO.Path.Combine(path, "master.key");
                System.IO.File.WriteAllText(filepath, masterKey);
                //
                HAContext.MasterKey = masterKey;
            }
        }

        /// <summary>
        /// 选举,返回选举出的编号， 未选中返回 零
        /// </summary>
        /// <param name="disableNum">禁止选中的编号(非master)</param>
        /// <returns></returns>
        public static int Elect(int disableNum)
        {
            int num = 0;

            //Choice , select first connection
            if (HAContext.Connectioned1 == true && disableNum != 1)
            {
                num = 1;
            }
            else if (HAContext.Connectioned2 == true && disableNum != 2)
            {
                num = 2;
            }
            //else if (HAContext.Connectioned3 == true && disableNum != 3)
            //{
            //    num = 3;
            //}

            //for (int i = 0; i < 3; i++)
            //{
            //    var client = this.clients[i];
            //    if (client != null && client.IsConnectioned == true)
            //    {
            //        index = i;
            //        break;
            //    }
            //}

            //
            return num;
        }

        /// <summary>
        /// 获取MASTER节点序号，无MASTER返回 0
        /// </summary>
        /// <returns></returns>
        public static int GetMasterNum()
        {
            var num = 0;

            if (HAContext.Connectioned1 == true && HAContext.MasterNum == 1)
            {
                num = 1;
            }
            else if (HAContext.Connectioned2 == true && HAContext.MasterNum == 2)
            {
                num = 2;
            }
            //else if (HAContext.Connectioned3 == true && HAContext.MasterNum == 3)
            //{
            //    num = 3;
            //}

            return num;
        }

        public static void PrintLog(Adf.LogWriter logWriter)
        {
            if (HAContext.Enable == true)
            {
                if (logWriter.Enable)
                {
                    logWriter.WriteTimeLine("HA:Node1: " + HAContext.Node1Point);
                    logWriter.WriteTimeLine("HA:Node2: " + HAContext.Node2Point);
                    logWriter.WriteTimeLine("HA:Node3: " + HAContext.Node3Point);
                    //
                    logWriter.WriteTimeLine("HA:Keepalive: " + (HAContext.Keepalive / 1000));
                    logWriter.WriteTimeLine("HA:ElectTimeout: " + (HAContext.ElectTimeout / 1000));
                    logWriter.WriteTimeLine("HA:MasterKey: " + HAContext.MasterKey);
                }
            }
        }

        //public static void LoadVip()
        //{
        //    var vip = Adf.ConfigHelper.GetSetting("HA:Vip", "");
        //    var vips = vip.Split(' ');
        //    var eth = "";
        //    var isIp = false;
        //    var isMask = false;
        //    var isGateway = false;
        //    if (vips.Length == 3)
        //    {
        //        isIp = Adf.ValidateHelper.IsIPv4(vips[0]);
        //        isMask = Adf.ValidateHelper.IsIPv4(vips[1]);
        //        isGateway = Adf.ValidateHelper.IsIPv4(vips[2]);
        //    }
        //    //
        //    var networkInterfaces = System.Net.NetworkInformation.NetworkInterface.GetAllNetworkInterfaces();
        //    for (int i = 0; i < networkInterfaces.Length; i++)
        //    {
        //        var properties = networkInterfaces[i].GetIPProperties();
        //        var count = properties.UnicastAddresses.Count;
        //        //
        //        for (int j = 0; j < count; j++)
        //        {
        //            var address = properties.UnicastAddresses[j];
        //            var ip = address.Address.ToString();
        //            if (ip == HAContext.SelfAddress)
        //            {
        //                eth = networkInterfaces[i].Name;
        //                break;
        //            }
        //        }
        //    }
        //    //
        //    HAContext.VipEnable = isIp && isMask && isGateway && eth != "";
        //    if (HAContext.VipEnable == true)
        //    {
        //        HAContext.Vip = vip;
        //        HAContext.VipEth = eth;
        //    }
        //}

        //public static void VipUp()
        //{
        //    var command = "netsh interface ip add address \"" + HAContext.VipEth + "\" static " + HAContext.Vip;
        //    //
        //    var p = new System.Diagnostics.Process();
        //    p.StartInfo.FileName = "cmd.exe";
        //    p.StartInfo.UseShellExecute = false;
        //    p.StartInfo.RedirectStandardInput = true;
        //    p.StartInfo.RedirectStandardOutput = false;
        //    p.StartInfo.RedirectStandardError = false;
        //    p.StartInfo.CreateNoWindow = true;
        //    p.Start();
        //    p.StandardInput.WriteLine(command);
        //    p.StandardInput.WriteLine("exit");
        //    p.WaitForExit();
        //}

        //public static void VipDown()
        //{
        //    // delete address "Local Area Connection" addr=10.0.0.1 gateway=all
        //    var command = "netsh interface ip add address \"" + HAContext.VipEth + "\" static " + HAContext.Vip;
        //    //
        //    var p = new System.Diagnostics.Process();
        //    p.StartInfo.FileName = "cmd.exe";
        //    p.StartInfo.UseShellExecute = false;
        //    p.StartInfo.RedirectStandardInput = true;
        //    p.StartInfo.RedirectStandardOutput = false;
        //    p.StartInfo.RedirectStandardError = false;
        //    p.StartInfo.CreateNoWindow = true;
        //    p.Start();
        //    p.StandardInput.WriteLine(command);
        //    p.StandardInput.WriteLine("exit");
        //    p.WaitForExit();
        //}
    }
}