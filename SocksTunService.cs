using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Linq;
using System.ServiceProcess;
using System.Text;
using SocksTun.Services;

namespace SocksTun
{
	public class SocksTunService
	{
		public SocksTunService()
		{
			debug.LogLevel = 2;
		}

		public void Run(string[] args)
		{
			Console.CancelKeyPress += Console_CancelKeyPress;
			debug.Writer = Console.Out;
			OnStart(args);
			debug.Log(-1, "SocksTun running in foreground mode, press enter to exit");
			Console.ReadLine();
			debug.Log(-1, "Shutting down...");
			OnStop();
		}

		static void Console_CancelKeyPress(object sender, ConsoleCancelEventArgs e)
		{
			e.Cancel = true;
		}

		private readonly DebugWriter debug = new DebugWriter();
		private readonly IDictionary<string, IService> services = new Dictionary<string, IService>();

		protected void OnStart(string[] args)
		{
			services["connectionTracker"] = new ConnectionTracker(debug, services);
			services["natter"] = new Natter(debug, services);
			services["transparentSocksServer"] = new TransparentSocksServer(debug, services);

            services["connectionTracker"].Start();
			services["natter"].Start();
			services["transparentSocksServer"].Start();

#if USEUDP
            services["transparentUdpServer"] = new TransparentUdpServer(debug, services);
            services["transparentUdpServer"].Start();
#endif

        }

        protected void OnStop()
		{
#if USEUDP
            services["transparentUdpServer"].Stop();
#endif

            services["transparentSocksServer"].Stop();
			services["natter"].Stop();
			services["connectionTracker"].Stop();
		}
	}
}
