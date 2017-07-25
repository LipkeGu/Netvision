using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Netvision.Backend.Network;
using System.Net;
using System.Threading;

namespace Netvision.Backend
{
	public enum BackendAction
	{
		Give,
		Take
	}

	public enum BackendTarget
	{
		Playlist,
		Epg,
		WebSite,
		Unknown
	}
	
	public class Backend
	{
		public delegate void BackendRequestEventHandler(object sender, BackendRequestEventArgs e);
		public event BackendRequestEventHandler BackendRequest;
		public class BackendRequestEventArgs : EventArgs
		{
			public HttpListenerContext Context;
			public BackendTarget Target;
			public BackendAction Action;
		}

		public delegate void WebSiteRequestEventHandler(object sender, WebSiteRequestEventArgs e);
		public event WebSiteRequestEventHandler WebSiteRequest;
		public class WebSiteRequestEventArgs : EventArgs
		{
			public HttpListenerContext Context;
		}

		Dictionary<string, HTTPSocket> HTTPSockets;
		bool running = false;

		Network.WebSite website;
		Network.Backend backend;

		public Backend(int port = 81)
		{
			HTTPSockets = new Dictionary<string, HTTPSocket>();

			HTTPSockets.Add("backend", new HTTPSocket("*", 81, string.Empty));
			HTTPSockets.Add("website", new HTTPSocket("*", 82, string.Empty));

			backend = new Network.Backend(this);
			backend.BackendResponse += (sender, e) => {
				HTTPSockets["backend"].Send(Encoding.UTF8.
					GetBytes(e.Response), ref e.Context);
			};

			HTTPSockets["backend"].DataReceived += (sender, e) =>
			{
				switch (e.Target)
				{
					case BackendTarget.Playlist:
					case BackendTarget.Epg:
						var evArgs = new BackendRequestEventArgs();
						evArgs.Context = e.Context;
						evArgs.Target = e.Target;

						BackendRequest?.Invoke(this, evArgs);

						break;
					case BackendTarget.WebSite:
					case BackendTarget.Unknown:
					default:
						e.Context.Response.StatusCode = 404;
						e.Context.Response.StatusDescription = "Not Found";
						HTTPSockets["backend"].Send(new byte[0], ref e.Context);
						break;
				}
			};

			website = new WebSite(this);
			website.WebSiteResponse += (sender, e) =>
			{
				HTTPSockets["website"].Send(Encoding.UTF8.
					GetBytes(e.Response), ref e.Context);
			};

			HTTPSockets["website"].DataReceived += (sender, e) =>
			{
				if (e.UserAgent.Contains("Kodi"))
				{
					e.Context.Response.StatusCode = 403;
					e.Context.Response.StatusDescription = "Forbidden";

					HTTPSockets["website"].Send(new byte[0], ref e.Context);

					return;
				}

				var evArgs = new WebSiteRequestEventArgs();
				evArgs.Context = e.Context;

				WebSiteRequest?.Invoke(this, evArgs);
			};

			running = true;
			var t = new Thread(HeartBeat);
			t.Start();
		}

		public void HeartBeat()
		{
			var interval = 60000;
			while (running)
			{
				Thread.Sleep(interval);

				backend.HeartBeat();
			}
			
		}
	}
}
