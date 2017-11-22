using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;

using Netvision.Backend.Provider;
using System.Threading.Tasks;
using System.IO;
using System.Text;

namespace Netvision.Backend
{
	public class PlayListProvider : IProvider
	{
		public delegate void PlayListProviderResponseEventHandler(object sender, PlayListProviderResponseEventArgs e);
		public event PlayListProviderResponseEventHandler PlayListProviderResponse;
		public class PlayListProviderResponseEventArgs : EventArgs
		{
			public BackendAction Action;
			public string Response;
			public int Provider;
			public HttpListenerContext Context;
			public Dictionary<string, string> Parameters;
		}

		SQLDatabase db;
		public PlayListProvider(ref SQLDatabase db, BackendHub backend)
		{
			this.db = db;
			backend.PlayListRequest += (sender, e) =>
			{
				switch (e.Action)
				{
					case BackendAction.Download:
						Task.Run(() => Download(e.Provider, e.Response, e.Parameters, e.Context));
						break;
					case BackendAction.Create:
						Task.Run(() => Create(e.Provider, e.Response, e.Parameters, e.Context));
						break;
					case BackendAction.Import:
						Task.Run(() => Import(e.Provider, e.Response, e.Parameters, e.Context));
						break;
					case BackendAction.Remove:
						Task.Run(() => Remove(e.Provider, e.Response, e.Parameters, e.Context));
						break;
					case BackendAction.Update:
						Task.Run(() => Update(e.Provider, e.Response, e.Parameters, e.Context));
						break;
					default:
						break;
				}
			};
		}

		async Task<bool> TestChannelUrl(string name, string url, string ua)
		{
			if (string.IsNullOrEmpty(url))
				return false;

			if (url.StartsWith("rtmp"))
				return true;

			var result = false;

			var hc = new HTTPClient(url, "HEAD");
			Task.Run(() => hc.GetResponse(name));
			hc.HTTPClientResponse += (sender, e) =>
			{
				switch (hc.StatusCode)
				{
					case HttpStatusCode.OK:
					case HttpStatusCode.Moved:
						result = true;
						break;
					default:
						result = false;
						break;
				}
			};

			return result;
		}

		async static Task<string> GetURLForChannel(SQLDatabase db, int id, bool heartbeat = false)
		{
			var list = new List<string>();
			lock (ChannelProvider.Channels.Members.Values)
			{
				var urls = (from s in ChannelProvider.Channels.Members.Values
							where s.ChanDBID == id
							where s.Servers.Count != 0
							select s).FirstOrDefault();

				if (urls != null)
				{
					for (var i = 0; i < urls.Servers.Count; i++)
					{
						var url = urls.Servers[i].URL;
						if (!string.IsNullOrEmpty(url))
							list.Add(url);
					}
				}
			}

			return list.ElementAt(new Random().Next(0, list.Count - 1));
		}

		public async void Create(int provider, string response,
			Dictionary<string, string> parameters, HttpListenerContext context)
		{
			var playlist = "#EXTM3U\r\n";
			var channels = ChannelProvider.GetChannels();

			if (channels.Count() != 0 && context != null)
				foreach (var channel in channels)
				{
					var server = channel.Servers[new Random().Next(0, (channel.Servers.Count - 1))].URL;
					var name = channel.Name;
					var logo = channel.Logo;
					var type = channel.Type;
					var epgid = channel.ID;

					var prov = ChannelProvider.GetProviderNameByID(db, provider);
					playlist += string.Format("#EXTINF:-1 tvg-name=\"{2}\" group-title=\"{3}\" tvg-id=\"{0}\",{2}\r\n",
						epgid == 0 ? string.Empty : epgid.AsString(), logo, name, prov.Result);

					playlist += string.Format("{0}\r\n", (type != 2 || !server.EndsWith(".ts")) ? server :
						string.Format("http://{1}:83/tsproxy/?url={0}", server, context.Request.Headers["Host"].Split(':')[0]));
				}

			Task.Run(() => Console.WriteLine("Playlist created!"));
			Task.Run(() => MakeResponse(provider, playlist, BackendAction.Create, parameters, context));
		}

		public async void Download(int provider, string response, Dictionary<string, string> parameters, HttpListenerContext context)
		{
			var ps = db.SQLQuery<ulong>(string.Format(
				"SELECT file, ua, url FROM playlist_urls WHERE skip='0' AND id = '{0}'",
				parameters["playlists"])).Result;

			if (ps.Count != 0)
			{
				for (var i = ulong.MinValue; i < (ulong)ps.Count; i++)
				{
					var ua = ps[i]["ua"];
					var file = ps[i]["file"];
					var url = ps[i]["url"];

					if (url.Contains("|"))
					{
						var urls = url.Split('|');
						if (urls.Length != 0)
						{
							var f = file.Split('.');

							for (var url_i = 0; url_i < urls.Length; url_i++)
							{
								var u = urls[url_i];
								if (u.Contains("[#COUNT#]"))
									 collectPlayLists(file, u, ua, provider);
								else
									_downloadPlayList(u, string.Format("{0}-{1}.{2}", f[0], url_i, f[1]), ua, provider);
							}
						}
					}
					else
					{
						if (url.Contains("[#COUNT#]"))
							collectPlayLists(file, url, ua, provider);
						else
							_downloadPlayList(url, file, ua, provider);
					}
				}
			}

			Task.Run(() => MakeResponse(provider, response, BackendAction.Download, parameters, context));
		}

		async void collectPlayLists(string file, string url, string ua, int provider)
		{
			var f = Functions.ReplaceAndFixNames(db, file).Result.Split('.');

			for (var ic = 0; ic < 10; ic++)
			{
				var u = Functions.ReplaceAndFixNames(db, (ic == 0) ?
					url.Replace("-[#COUNT#]", string.Empty) : url.Replace("[#COUNT#]", string.Format("{0}", ic)), false).Result;

				_downloadPlayList(u, string.Format("{0}-{1}.{2}", f[0], ic, f[1]), ua, provider);
			}
		}

		async void _downloadPlayList(string url, string file, string ua, int provider)
		{
			var importDir = Path.Combine(Directory.GetCurrentDirectory(),
				string.Format("Download/playlist/{0}", provider));

			var u = Functions.ReplaceAndFixNames(db, url, false);
			var dl = new HTTPClient(u.Result);
			dl.UserAgent = ua;

			dl.GetResponse(url);
			dl.HTTPClientResponse += (sender, e) =>
			{
				if (e.Data.Length != 0)
				{
					using (var fs = new Filesystem(importDir))
						fs.Write(Filesystem.Combine(fs.Root, file), e.Data);

					Task.Run(() => Console.WriteLine("Downloaded Playlist: {0}...", u.Result));
				}
			};
		}

		public async void Import(int provider, string response, Dictionary<string, string> parameters, HttpListenerContext context)
		{
			Task.Run(() => MakeResponse(provider, response, BackendAction.Import, parameters, context));
		}

		public async void Update(int provider, string response, Dictionary<string, string> parameters, HttpListenerContext context)
		{
			foreach (var c in (from c in ChannelProvider.GetChannels() where c.Servers.Count != 0 select c).AsEnumerable())
				foreach (var s in (from s in c.Servers where !s.URL.StartsWith("rtmp") select s).AsEnumerable())
					if (!TestChannelUrl(c.Name, s.URL, Functions.GetUseragentByID(db, s.UserAgent).Result).Result)
						db.SQLInsert(string.Format("DELETE FROM servers WHERE url='{0}'", s.URL)).ConfigureAwait(false);

			Task.Run(() => MakeResponse(provider, string.Empty, BackendAction.Update, parameters, context));
		}

		public void MakeResponse(int provider, string response, BackendAction action,
			Dictionary<string, string> parameters, HttpListenerContext context)
		{
			var evArgs = new PlayListProviderResponseEventArgs();
			evArgs.Provider = provider;
			evArgs.Action = action;
			evArgs.Response = response;
			evArgs.Context = context;
			evArgs.Parameters = parameters;

			PlayListProviderResponse?.Invoke(this, evArgs);
		}

		public void Remove(int provider, string response, Dictionary<string, string> parameters, HttpListenerContext context)
		{
		}

		public void Add(int provider, string response, Dictionary<string, string> parameters, HttpListenerContext context)
		{
		}
	}
}
