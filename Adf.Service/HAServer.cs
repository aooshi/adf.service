using System;
using System.Net;

namespace Adf.Service
{
    class HAServer : IDisposable
    {
        private ServiceContext serviceContext;
        private Adf.LogManager logManager;
        private HttpServer server;
        private Version version;

        public HAServer(ServiceContext serviceContext)
        {
            this.version = this.GetType().Assembly.GetName().Version;
            this.serviceContext = serviceContext;
            this.logManager = serviceContext.LogManager;
            //
            this.Initialize();
        }

        private void Initialize()
        {
            IPEndPoint ep = null;
            if (HAContext.SelfNum == 1)
            {
                ep = HAContext.Node1;
            }
            else if (HAContext.SelfNum == 2)
            {
                ep = HAContext.Node2;
            }
            else if (HAContext.SelfNum == 3)
            {
                ep = HAContext.Node3;
            }
            else
            {
                throw new Adf.ConfigException("ha configuration node invalid.");
            }
            //
            this.server = new HttpServer(ep.Port, ep.Address.ToString());
            this.server.WebSocketConnectioned += new HttpServerWebSocketCallback(WebSocketConnectioned);
            this.server.WebSocketDisconnected += new HttpServerWebSocketCallback(WebSocketDisconnected);
            this.server.WebSocketNewMessage += new HttpServerWebSocketMessage(WebSocketNewMessage);
            this.server.Callback = this.HttpProcess;
            this.server.Start();
        }

        private void WebSocketDisconnected(HttpServerWebSocketContext context)
        {
        }

        private void WebSocketConnectioned(HttpServerWebSocketContext context)
        {
            if (context.Path == "/ha")
            {
                var address = context.GetRemotePoint().Address.ToString();
                var num = 0;
                if (address == HAContext.Node1Address)
                {
                    num = 1;
                }
                else if (address == HAContext.Node2Address)
                {
                    num = 2;
                }
                else if (address == HAContext.Node3Address)
                {
                    num = 3;
                }
                //
                context.UserState = new ConnectionState()
                {
                    ip = context.GetRemotePoint().Address.ToString(),
                    num = num
                };
            }
        }

        private void WebSocketNewMessage(HttpServerWebSocketContext context, WebSocketMessageEventArgs args)
        {
            var state = context.UserState as ConnectionState;
            if (state == null)
                return;

            if (args.Opcode == WebSocketOpcode.Text)
            {
                var messageItems = args.Message.Split(':');
                if (messageItems[0] == "elect" && messageItems.Length == 3)
                {
                    //req ,    elect:xxxxx:masterkey
                    //resp,    elect:xxxxx:num
                    var num = HAContext.GetMasterNum();
                    if (num == 0)
                    {
                        var masterKey = messageItems[2];
                        var disabledNum = 0;
                        if (state.num == 1 && HAContext.MasterKey != masterKey)
                        {
                            disabledNum = 1;
                        }
                        else if (state.num == 2 && HAContext.MasterKey != masterKey)
                        {
                            disabledNum = 2;
                        }
                        //
                        num = HAContext.Elect(disabledNum);
                    }
                    //
                    if (num == 1 || num == 2 || num == 3)
                    {
                        this.logManager.Message.WriteTimeLine("HAServer response elect node " + num + " to " + state.ip);

                        try
                        {
                            context.SendAsync("elect:" + messageItems[1] + ":" + num, null);
                        }
                        catch (System.IO.IOException) { }
                        catch (System.Net.Sockets.SocketException) { }
                        catch (System.Exception)
                        {
                        }
                    }
                    //else
                    //{
                    //    //discard message
                    //}
                }
                else if (messageItems[0] == "readmasterkey")
                {
                    this.logManager.Message.WriteTimeLine("HAServer send master key " + HAContext.MasterKey + " to " + state.ip);
                    //
                    try
                    {
                        context.SendAsync("masterkey:" + HAContext.MasterKey, null);
                    }
                    catch (System.IO.IOException) { }
                    catch (System.Net.Sockets.SocketException) { }
                    catch (System.Exception)
                    {
                    }
                }
            }
        }

        private System.Net.HttpStatusCode HttpProcess(HttpServerContext httpContext)
        {
            var nowTick = Adf.UnixTimestampHelper.ToInt64Timestamp();

            //
            var servername = this.serviceContext.ServiceName;
            var hostname = System.Net.Dns.GetHostName();
            //
            var build = new System.Text.StringBuilder();
            build.AppendLine("<!DOCTYPE html>");
            build.AppendLine("<html>");
            build.AppendLine("<head>");

            build.AppendLine("<style type=\"text/css\">");
            build.AppendLine(".tb1{ background-color:#D5D5D5;}");
            build.AppendLine(".tb1 td{ background-color:#FFF;}");
            build.AppendLine(".tb1 tr.None td{ background-color:#FFF;}");
            build.AppendLine(".tb1 tr.Success td{ background-color:#FFF;}");
            build.AppendLine(".tb1 tr.Failed td{ background-color:#FAEBD7;}");
            build.AppendLine(".tb1 tr.Running td{ background-color:#F5FFFA;}");
            build.AppendLine("img,form{ border:0px;}");
            build.AppendLine("img.button{ cursor:pointer; }");
            build.AppendLine("a { padding-left:5px; }");
            build.AppendLine("</style>");

            build.AppendLine("<meta http-equiv=\"content-type\" content=\"text/html;charset=utf-8\">");
            build.AppendLine("<title>" + servername + " Via " + hostname + "</title>");
            build.AppendLine("</head>");
            build.AppendLine("<body>");

            build.AppendLine("<div>");
            build.AppendLine("<span>");
            build.AppendLine("Powered by <a href=\"http://www.aooshi.org/adf?project=" + servername + "\" target=\"_blank\">" + servername + "</a> ");
            build.Append('v');
            build.Append(version.Major);
            build.Append(".");
            build.Append(version.Minor);
            build.Append(".");
            build.Append(version.Build);

            build.AppendLine(" Via " + hostname);
            build.AppendLine("</span></div>");

            build.AppendLine("<table class=\"tb1\" width=\"100%\" border=\"0\" cellspacing=\"1\" cellpadding=\"3\">");
            build.AppendLine("<thead>");
            build.AppendLine("<tr>");
            build.AppendLine("<th align=\"left\" width=\"35\">No.</th>");
            build.AppendLine("<th align=\"left\">Node</th>");
            build.AppendLine("<th width=\"120\">Type</th>");
            build.AppendLine("<th width=\"160\">State</th>");
            build.AppendLine("</tr>");
            build.AppendLine("</thead>");
            build.AppendLine("<tbody>");


            build.AppendLine("<tr>");
            build.AppendLine("<td align=\"left\">1.</td>");
            build.AppendLine("<td align=\"left\">" + HAContext.Node1Point + "</td>");
            build.AppendLine("<td align=\"center\">" + this.GetNodeType(1) + "</td>");
            build.AppendLine("<td align=\"center\">" + (HAContext.Connectioned1 ? "connectioned" : "disconnectioned") + "</td>");
            build.AppendLine("</tr>");


            build.AppendLine("<tr>");
            build.AppendLine("<td align=\"left\">2.</td>");
            build.AppendLine("<td align=\"left\">" + HAContext.Node2Point + "</td>");
            build.AppendLine("<td align=\"center\">" + this.GetNodeType(2) + "</td>");
            build.AppendLine("<td align=\"center\">" + (HAContext.Connectioned2 ? "connectioned" : "disconnectioned") + "</td>");
            build.AppendLine("</tr>");


            build.AppendLine("<tr>");
            build.AppendLine("<td align=\"left\">3.</td>");
            build.AppendLine("<td align=\"left\">" + HAContext.Node3Point + "</td>");
            //build.AppendLine("<td align=\"center\">" + (HAContext.MasterNum == 3 ? "master" : "slave") + "</td>");
            build.AppendLine("<td align=\"center\">witness</td>");
            build.AppendLine("<td align=\"center\">" + (HAContext.Connectioned3 ? "connectioned" : "disconnectioned") + "</td>");
            build.AppendLine("</tr>");

            //
            build.AppendLine("<tr>");
            build.Append("<td colspan=\"4\" align=\"left\">MasterKey: " + HAContext.MasterKey);
            build.AppendLine("</td>");
            build.AppendLine("</tr>");

            //
            build.AppendLine("<tr>");
            build.AppendLine("<td colspan=\"4\" align=\"right\">");

            var stateArray = Enum.GetNames(typeof(ServiceState));
            var state = this.serviceContext.ServiceState.ToString();
            for (int i = 0, l = stateArray.Length; i < l; i++)
            {
                if (i > 0)
                {
                    build.Append(" | ");
                }
                //
                if (state == stateArray[i])
                {
                    build.Append("<strong>" + stateArray[i] + "</strong>");
                }
                else
                {
                    build.Append(stateArray[i]);
                }
            }
            build.AppendLine();
            build.AppendLine("</td>");
            build.AppendLine("</tr>");
            
            //
            build.AppendLine("</tbody>");
            build.AppendLine("</table>");
            
            //
            build.AppendLine("</body>");
            build.AppendLine("</html>");

            //
            httpContext.Content = build.ToString();
            httpContext.ResponseHeader["Via"] = hostname;
            httpContext.ResponseHeader["Content-Type"] = "text/html";

            return System.Net.HttpStatusCode.OK;
        }

        private string GetNodeType(int num)
        {
            if (HAContext.MasterNum == 0)
                return "---";

            return (num == HAContext.MasterNum) ? "master" : "slave";
        }

        public void Dispose()
        {
            if (this.server != null)
            {
                this.server.Stop();
                this.server.Dispose();
            }
        }

        class ConnectionState
        {
            public string ip;
            public int num;
        }
    }
}