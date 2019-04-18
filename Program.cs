using SocksTun.Properties;
using System;
using System.Collections.Generic;
using System.Configuration.Install;
using System.Diagnostics;
using System.Linq;
using System.Net.NetworkInformation;
using System.Reflection;
using System.ServiceProcess;
using System.Text;

namespace SocksTun
{
	static class Program
	{
		/// <summary>
		/// The main entry point for the application.
		/// </summary>
		static void Main(string[] args)
		{
            Debug.Assert(NetworkInterface.GetIsNetworkAvailable() == true);

            RouterHelper.SetupDefaultGateway(Settings.Default.AdapterName);

            (new SocksTunService()).Run(args);

            RouterHelper.Cleanup();
            return;
#if false
            if (args.Length > 0)
			{
				switch (args[0].ToLower())
				{
					case "--foreground":
					case "/foreground":
					case "-f":
					case "/f":
						(new SocksTunService()).Run(args);
						return;
					case "--install":
					case "/install":
					case "-i":
					case "/i":
						ManagedInstallerClass.InstallHelper(new[] {Assembly.GetEntryAssembly().Location});
						return;
					case "--uninstall":
					case "/uninstall":
					case "-u":
					case "/u":
						ManagedInstallerClass.InstallHelper(new[] { "/uninstall", Assembly.GetEntryAssembly().Location });
						return;
					default:
						Console.WriteLine("Unknown command line parameters: " + string.Join(" ", args));
						return;
				}
			}

			ServiceBase[] ServicesToRun;
			ServicesToRun = new ServiceBase[] 
			{ 
				new SocksTunService() 
			};
			ServiceBase.Run(ServicesToRun);
#endif
		}
	}
}
