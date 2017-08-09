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
		Create,
		Import,
		Remove,
		Update,
		Download,
		DownloadMetadata,
		HeartBeat
	}

	public enum BackendTarget
	{
		Playlist,
		Epg,
		Channel,
		WebSite,
		Unknown
	}

	public class Backend
	{
		SQLDatabase db;

		public delegate void BackendRequestEventHandler(object sender, BackendRequestEventArgs e);
		public event BackendRequestEventHandler BackendRequest;
		public event BackendRequestEventHandler WebSiteRequest;
		public class BackendRequestEventArgs : EventArgs
		{
			public HttpListenerContext Context;
			public BackendTarget Target;
			public BackendAction Action;
			public Dictionary<string, string> Parameters;
		}
		
		Dictionary<string, HTTPSocket> HTTPSockets;
		bool running = false;

		Network.WebSite website;
		Network.Backend backend;

		public Backend(int port = 81)
		{
			db = new SQLDatabase("channels.db");

			HTTPSockets = new Dictionary<string, HTTPSocket>();

			HTTPSockets.Add("backend", new HTTPSocket("*", 81, string.Empty));
			HTTPSockets.Add("website", new HTTPSocket("*", 82, string.Empty));

			backend = new Network.Backend(ref db, this);
			backend.BackendResponse += (sender, e) =>
			{
				if (e.Context == null)
					return;

				switch (e.Target)
				{
					case BackendTarget.Playlist:
						e.Context.Response.StatusCode = 200;
						e.Context.Response.StatusDescription = "OK";
						e.Context.Response.ContentType = "application/x-mpegURL";
						break;
					case BackendTarget.Epg:
						e.Context.Response.StatusCode = 200;
						e.Context.Response.StatusDescription = "OK";
						e.Context.Response.ContentType = "application/gzip";
						break;
					case BackendTarget.Channel:
						break;
					case BackendTarget.WebSite:
					case BackendTarget.Unknown:
					default:
						break;
				}

				Task.Run(() => HTTPSockets["backend"].Send(
					Encoding.UTF8.GetBytes(e.Response), e.Context)
					);
			};

			HTTPSockets["backend"].DataReceived += (sender, e) =>
			{
				switch (e.Target)
				{
					case BackendTarget.Playlist:
						var evArgs = new BackendRequestEventArgs();
						evArgs.Context = e.Context;
						evArgs.Target = e.Target;
						evArgs.Parameters = e.Parameters;

						BackendRequest?.Invoke(this, evArgs);
						break;
					case BackendTarget.Epg:
						using (var fs = new Filesystem(""))
						{
							if (fs.Exists("epgdata.xml.gz"))
							{
								var data = fs.Read("epgdata.xml.gz").Result;

								e.Context.Response.ContentType = "application/gzip";
								e.Context.Response.Headers.Add("Content-Disposition",
									"attachment; filename=epgdata.xml.gz");

								e.Context.Response.StatusCode = 200;
								e.Context.Response.StatusDescription = "OK";

								HTTPSockets["backend"].Send(data, e.Context);
							}
							else
							{
								e.Context.Response.StatusCode = 503;
								e.Context.Response.StatusDescription = "Service Unavailable";

								Task.Run(() => HTTPSockets["backend"].Send(new byte[0], e.Context));
							}
						}

						break;
					case BackendTarget.WebSite:
					case BackendTarget.Unknown:
					default:
						e.Context.Response.StatusCode = 404;
						e.Context.Response.StatusDescription = "Not Found";
						Task.Run(() => HTTPSockets["backend"].Send(e.Context.Response.StatusDescription, e.Context));
						break;
				}
			};

			website = new WebSite(this);
			website.WebSiteResponse += (sender, e) =>
			{
				HTTPSockets["website"].Send(Encoding.UTF8.
					GetBytes(e.Response), e.Context);
			};

			HTTPSockets["website"].DataReceived += (sender, e) =>
			{
				if (e.UserAgent.Contains("Kodi"))
				{
					e.Context.Response.StatusCode = 403;
					e.Context.Response.StatusDescription = "Forbidden";

					HTTPSockets["website"].Send(new byte[0], e.Context);

					return;
				}

				var evArgs = new BackendRequestEventArgs();
				evArgs.Context = e.Context;
				evArgs.Parameters = e.Parameters;
				evArgs.Target = e.Target;

				WebSiteRequest?.Invoke(this, evArgs);
			};

			running = true;
			var t = new Thread(HeartBeat);
			t.IsBackground = true;

			t.Start();
		}

		public void HeartBeat()
		{
			var interval = 60000;
			while (running)
			{
				Thread.Sleep(interval);
				db.HeartBeat();

				if (DateTime.Now.Hour == 2 || DateTime.Now.Hour == 8 ||
				DateTime.Now.Hour == 14 || DateTime.Now.Hour == 20)
				{
					if (DateTime.Now.Minute < 3)
						backend?.HeartBeat();
				}
			}
		}
	}
}
