using System;
using System.Collections.Generic;
using System.ServiceProcess;
using System.Text;
using System.Configuration.Install;
using System.Reflection;
using System.Configuration;
using System.Collections;

namespace Adf.Service
{
    /// <summary>
    /// service helper
    /// </summary>
    public class ServiceHelper
    {
        const int SERVICE_CONTROL_TIMEOUT = 90;

        #region entry
        /// <summary>
        /// 应用程序的主入口点。
        /// </summary>
        /// <param name="args"></param>
        public static int Entry(string[] args)
        {
            string serviceName;

            if (!string.IsNullOrEmpty(ConfigurationManager.AppSettings["ServiceName"]))
                serviceName = ConfigurationManager.AppSettings["ServiceName"];
            else
            {
                //serviceName = System.Reflection.Assembly.GetEntryAssembly().GetName().Name;
                var path = System.Reflection.Assembly.GetEntryAssembly().Location;
                var name = System.IO.Path.GetFileNameWithoutExtension(path);
                serviceName = name;
            }


            if (args.Length == 0)
            {
                RunService(serviceName);
            }
            else if (args[0].Equals("/i", StringComparison.OrdinalIgnoreCase))
            {
                ServiceHelper.Install(serviceName);
            }
            else if (args[0].Equals("/u", StringComparison.OrdinalIgnoreCase))
            {
                ServiceHelper.UnInstall(serviceName);
            }
            else if (args[0].Equals("/c", StringComparison.OrdinalIgnoreCase))
            {
                RunConsole(serviceName,args);
            }
            else
            {
                Console.WriteLine("unknown parameter: {0}, support: /i,/u,/c", args[0]);
                Console.Read();
            }
            return 0;
        }


        static void RunService(string serviceName)
        {
            var ServicesToRun = new System.ServiceProcess.ServiceBase[] 
			{ 
				new Service(serviceName)
			};
            System.ServiceProcess.ServiceBase.Run(ServicesToRun);
        }

        static void RunConsole(string serviceName, string[] args)
        {
            Console.WriteLine("Press key 'q' to stop.");
            var context = new ServiceContext(serviceName, true);
            try
            {
                context.Start(args);
                while (Console.ReadKey().Key != ConsoleKey.Q)
                {
                    Console.WriteLine(",Press key 'q' to stop.");
                    continue;
                };
            }
            finally
            {
                context.Stop();
                context.Destroy();
            }
        }
        #endregion

        #region install
        static bool ServiceIsExisted(string serviceName)
        {
            ServiceController[] services = ServiceController.GetServices();
            foreach (ServiceController s in services)
            {
                if (s.ServiceName == serviceName)
                {
                    return true;
                }
            }
            return false;
        }

        static void UnInstall(string serviceName)
        {
            if (!ServiceIsExisted(serviceName))
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine(@"/***************
service no install
/***************
");
                Console.ResetColor();
            }
            else
            {
                if (StopService(serviceName))
                    ServiceInstall(serviceName, false);
                //ManagedInstallerClass.InstallHelper(new string[] { "/u", GetAssemblyPath() });
            }
        }

        static void Install(string serviceName)
        {
            if (ServiceIsExisted(serviceName))
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine(@"/***************
service existed
/***************
");
                Console.ResetColor();
            }
            else
            {
                //ManagedInstallerClass.InstallHelper(new string[] { GetAssemblyPath() });
                ServiceInstall(serviceName, true);

                if (ConfigurationManager.AppSettings["ServiceInstalledStart"] != "false")
                    StartService(serviceName);
            }
        }

        static bool StartService(string serviceName)
        {
            if (ServiceIsExisted(serviceName))
            {
                System.ServiceProcess.ServiceController service = new System.ServiceProcess.ServiceController(serviceName);
                if (service.Status != System.ServiceProcess.ServiceControllerStatus.Running &&
                    service.Status != System.ServiceProcess.ServiceControllerStatus.StartPending)
                {
                    service.Start();
                    for (int i = 0; i < SERVICE_CONTROL_TIMEOUT; i++)
                    {
                        service.Refresh();
                        if (service.Status == System.ServiceProcess.ServiceControllerStatus.Running)
                        {
                            Console.WriteLine("Service Started");
                            return true;
                        }
                        System.Threading.Thread.Sleep(1000);
                    }
                    Console.WriteLine("Start Service Timeout");
                    return false;
                }
                else
                {
                    return true;
                }
            }
            return false;
        }

        static bool StopService(string serviceName)
        {
            if (ServiceIsExisted(serviceName))
            {
                System.ServiceProcess.ServiceController service = new System.ServiceProcess.ServiceController(serviceName);
                if (service.Status == System.ServiceProcess.ServiceControllerStatus.Running)
                {
                    service.Stop();
                    for (int i = 0; i < SERVICE_CONTROL_TIMEOUT; i++)
                    {
                        service.Refresh();
                        if (service.Status == System.ServiceProcess.ServiceControllerStatus.Stopped)
                        {
                            Console.WriteLine("Service Stoped");
                            return true;
                        }
                        System.Threading.Thread.Sleep(1000);
                    }
                    Console.WriteLine("Stop Service Timeout");
                    return false;
                }
                else
                {
                    return true;
                }
            }
            return false;
        }

        //static string GetAssemblyPath()
        //{
        //    //return Assembly.GetExecutingAssembly().Location;
        //    return Assembly.GetEntryAssembly().Location;
        //}

        #endregion

        #region service install
        //see: https://msdn.microsoft.com/zh-cn/library/system.configuration.install.installcontext%28v=vs.80%29.aspx

        static void ServiceInstall(string serviceName, bool install)
        {
            //config
            var username = ConfigurationManager.AppSettings["ServiceUsername"];
            var password = ConfigurationManager.AppSettings["ServicePassword"];
            var ServiceDepended = ConfigurationManager.AppSettings["ServiceDepended"];
            var description = ConfigurationManager.AppSettings["ServiceDescription"];

            //account
            ServiceAccount account = ServiceAccount.LocalSystem;
            if (!string.IsNullOrEmpty(username))
            {
                switch (username)
                {
                    case "LocalService":
                        account = System.ServiceProcess.ServiceAccount.LocalService;
                        break;
                    case "LocalSystem":
                        account = System.ServiceProcess.ServiceAccount.LocalSystem;
                        break;
                    case "NetworkService":
                        account = System.ServiceProcess.ServiceAccount.NetworkService;
                        break;
                    default:
                        account = System.ServiceProcess.ServiceAccount.User;
                        break;
                }
            }

            //account
            TransactedInstaller ti = new TransactedInstaller();
            if (account == ServiceAccount.User)
            {
                ti.Installers.Add(new ServiceProcessInstaller
                {
                    Account = ServiceAccount.User
                    ,
                    Username = username
                    ,
                    Password = password
                });
            }
            else
            {
                ti.Installers.Add(new ServiceProcessInstaller
                {
                    Account = account
                });
            }

            //service config
            ti.Installers.Add(new ServiceInstaller
            {
                ServiceName = serviceName,
                DisplayName = serviceName,
                Description = string.IsNullOrEmpty(description) ? "" : description,
                ServicesDependedOn = string.IsNullOrEmpty(ServiceDepended) ? new string[0] : ServiceDepended.Split(';'),
                StartType = ServiceStartMode.Automatic
            });

            //context
            ti.Context = new InstallContext();
            ti.Context.Parameters["assemblypath"] = Assembly.GetEntryAssembly().Location;

            //"\"" + Assembly.GetEntryAssembly().Location + "\" /service";
            //ti.Context.Parameters["assemblypath"] = "\""+ Assembly.GetEntryAssembly().Location + "\"";// /ServiceName=" + serviceName;
            //ti.Context.Parameters["CommandLine"] = "/ServiceName=" + serviceName;

            if (install)
            {
                var installState = new Hashtable();
                // Call the 'Install' method.
                ti.Install(installState);
            }
            else
            {
                // Call the 'UnInstall' method.
                ti.Uninstall(null);
            }
        }
        #endregion
    }
}