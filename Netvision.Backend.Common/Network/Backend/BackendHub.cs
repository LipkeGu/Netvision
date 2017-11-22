using System;
using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;

namespace Netvision.Backend
{
	public class BackendHub
	{
		public delegate void BackendHubResponseEventHandler(object sender, BackendHubResponseEventArgs e);
		public event BackendHubResponseEventHandler BackendHubResponse;
		public class BackendHubResponseEventArgs : EventArgs
		{
			public HttpListenerContext Context;
			public string Response;
			public BackendTarget Target;
		}

		public delegate void ProviderRequestEventHandler(object sender, ProviderRequestEventArgs e);
		public event ProviderRequestEventHandler ChannelRequest;
		public event ProviderRequestEventHandler EPGRequest;
		public event ProviderRequestEventHandler PlayListRequest;

		public class ProviderRequestEventArgs : EventArgs
		{
			public BackendAction Action;
			public HttpListenerContext Context;
			public Dictionary<string, string> Parameters;
			public int Provider;
			public string Response;
		}

		PlayListProvider playlist;
		EPGProvider epg;
		ChannelProvider channel;
		
		public BackendHub(ref SQLDatabase db, Network.Backend backend)
		{
			channel = new ChannelProvider(this);
			channel.ChannelProviderResponse += (sender, e) =>
			{
				var evArgs = new BackendHubResponseEventArgs();
				evArgs.Response = e.Response;
				evArgs.Context = e.context;
				evArgs.Target = BackendTarget.Channel;

				switch (e.Action)
				{
					case BackendAction.Create:
						MakeRequest(BackendTarget.Channel, BackendAction.Import, e.context, e.Parameters, e.Response, e.Provider);
						break;
					case BackendAction.Import:
						MakeRequest(BackendTarget.Epg, BackendAction.Download, e.context, e.Parameters, e.Response, e.Provider);
						break;
					case BackendAction.Remove:
						MakeRequest(BackendTarget.Epg, BackendAction.Remove, e.context, e.Parameters, e.Response, e.Provider);
						break;
					case BackendAction.Update:
						MakeRequest(BackendTarget.Epg, e.Action, e.context, e.Parameters, e.Response, e.Provider);
						break;
					case BackendAction.Download:
						MakeRequest(BackendTarget.Playlist, BackendAction.Download, e.context, e.Parameters, e.Response, e.Provider);
						break;
					default:
						break;
				}
			};

			playlist = new PlayListProvider(ref db, this);
			playlist.PlayListProviderResponse += (sender, e) =>
			{
				var evArgs = new BackendHubResponseEventArgs();
				evArgs.Response = e.Response;
				evArgs.Context = e.Context;
				evArgs.Target = BackendTarget.Playlist;

				switch (e.Action)
				{
					case BackendAction.Create:
						if (e.Context != null)
							BackendHubResponse?.Invoke(this, evArgs);
						break;
					case BackendAction.Download:
						MakeRequest(BackendTarget.Channel, BackendAction.Create, e.Context, e.Parameters, e.Response, e.Provider);
						break;
					case BackendAction.Import:
						MakeRequest(BackendTarget.Channel, BackendAction.Import, e.Context, e.Parameters, e.Response, e.Provider);
						break;
					case BackendAction.Remove:
						BackendHubResponse?.Invoke(this, evArgs);
						break;
					case BackendAction.Update:
						MakeRequest(BackendTarget.Playlist, BackendAction.Create, e.Context, e.Parameters, e.Response, e.Provider);
						break;
					default:
						break;
				}
			};

			epg = new EPGProvider(ref db, this);
			epg.EPGProviderResponse += (sender, e) =>
			{
				var evArgs = new BackendHubResponseEventArgs();
				evArgs.Response = e.Response;
				evArgs.Context = e.Context;
				evArgs.Target = BackendTarget.Epg;

				switch (e.Action)
				{
					case BackendAction.Create:
						MakeRequest(BackendTarget.Playlist, BackendAction.Create, e.Context, e.Parameters, e.Response, e.Provider);
						break;
					case BackendAction.Import:
						MakeRequest(BackendTarget.Playlist, BackendAction.Update, e.Context, e.Parameters, e.Response, e.Provider);
						break;
					case BackendAction.Remove:
						MakeRequest(BackendTarget.Playlist, BackendAction.Remove, e.Context, e.Parameters, e.Response, e.Provider);
						break;
					case BackendAction.Update:
						MakeRequest(BackendTarget.Playlist, BackendAction.Update, e.Context, e.Parameters, e.Response, e.Provider);
						break;
					case BackendAction.Download:
						MakeRequest(BackendTarget.Epg, BackendAction.Create, e.Context, e.Parameters, e.Response, e.Provider);
						break;
					default:
						break;
				}
			};

			backend.BackendHubRequest += (sender, e) =>
			{
				MakeRequest(e.Target, e.Action, e.Context, e.Parameters, e.Response, 0);
			};

			var providers = db.SQLQuery<uint>("SELECT * FROM providers WHERE id !='0' AND skip !='1'").Result;
			if (providers.Count != 0)
			{
				for (var i = uint.MinValue; i < providers.Count; i++)
				{
					var x = new Dictionary<string, string>();
					x.Add("chanlist", providers[i]["channel_list"]);
					x.Add("logolist", providers[i]["logo_list"]);
					x.Add("epglist", providers[i]["epg_list"]);
					x.Add("playlists", providers[i]["playlist_urls"]);

					var clist = providers[i]["channel_list"];
					var provider = int.Parse(providers[i]["id"]);

					Task.Run(() => MakeRequest((clist != "0") ? BackendTarget.Channel :
						BackendTarget.Playlist, BackendAction.Download, null, x, string.Empty, provider));
				}
			}
			else
				Task.Run(() => MakeRequest(BackendTarget.Playlist, BackendAction.Download,
					null, new Dictionary<string, string>(), string.Empty, 0));
		}

		void MakeRequest(BackendTarget target, BackendAction action, HttpListenerContext context,
			Dictionary<string,string> parameters, string response, int provider)
		{
			var evArgs = new ProviderRequestEventArgs();
			evArgs.Context = context;
			evArgs.Parameters = parameters;
			evArgs.Action = action;
			evArgs.Provider = provider;

			switch (target)
			{
				case BackendTarget.Playlist:
					evArgs.Response = response;
					PlayListRequest?.Invoke(this, evArgs);
					break;
				case BackendTarget.Epg:
					evArgs.Response = response;
					EPGRequest?.Invoke(this, evArgs);
					break;
				case BackendTarget.Channel:
					evArgs.Response = response;
					ChannelRequest?.Invoke(this, evArgs);
					break;
				case BackendTarget.WebSite:
				case BackendTarget.Unknown:
				default:
					break;
			}
		}

		public void HeartBeat() => MakeRequest(BackendTarget.Channel, BackendAction.Update, null,
			new Dictionary<string, string>(), string.Empty, 1);
	}
}
