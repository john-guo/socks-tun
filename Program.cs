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

            NetworkHelper.SetupDefaultGateway();

            (new SocksTunService()).Run(args);

            NetworkHelper.Cleanup();
            return;
		}
	}
}
