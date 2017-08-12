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
						Download(e.Provider, e.Response, e.Parameters, e.Context);
						break;
					case BackendAction.Create:
						Create(e.Provider, e.Response, e.Parameters, e.Context);
						break;
					case BackendAction.Import:
						Import(e.Provider, e.Response, e.Parameters, e.Context);
						break;
					case BackendAction.Remove:
						Remove(e.Provider, e.Response, e.Parameters, e.Context);
						break;
					case BackendAction.Update:
						Update(e.Provider, e.Response, e.Parameters, e.Context);
						break;
					default:
						break;
				}
			};
		}

		async Task<bool> TestChannelUrl(string url, string ua)
		{
			if (string.IsNullOrEmpty(url))
				return false;

			if (url.StartsWith("rtmp"))
				return true;

			var result = false;

			var hc = new HTTPClient(url, "HEAD");
			await hc.GetResponse();

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

			return result;
		}

		async static Task<string> GetURLForChannel(SQLDatabase db, int id, bool heartbeat = false)
		{
			var list = new List<string>();
			var urls = (from s in ChannelProvider.Channels.Members.Values
						where s.ChanDBID == id
						where s.Servers.Count != 0
						select s).FirstOrDefault();

			if (urls != null)
			{
				await Task.Run(() =>
				{
					for (var i = 0; i < urls.Servers.Count; i++)
						if (!string.IsNullOrEmpty(urls.Servers[i].URL))
							list.Add(urls.Servers[i].URL);
				});
			}

			return list.ElementAt(new Random().Next(0, list.Count));
		}

		public async void Create(int provider, string response,
			Dictionary<string, string> parameters, HttpListenerContext context)
		{
			var playlist = "#EXTM3U\r\n";
			var channels = ChannelProvider.GetChannels();

			foreach (var channel in channels)
			{
				var server = channel.Servers[new Random().Next(0, (channel.Servers.Count - 1))].URL;
				var name = channel.Name;
				var logo = channel.Logo;

				var logo_url = await ChannelProvider.GetLogoURLByProvider(db, provider);
				if (string.IsNullOrEmpty(logo_url) || string.IsNullOrEmpty(logo))
					logo_url = string.Empty;

				var epgid = channel.ID;
				var prov = await ChannelProvider.GetProviderNameByID(db, provider);

				playlist += string.Format("#EXTINF:-1 tvg-name=\"{2}\" group-title=\"{3}\" tvg-id=\"{0}\" tvg-logo=\"{4}{1}\",{2}\r\n",
					epgid == 0 ? string.Empty : epgid.AsString(), logo, name, prov, logo_url);

				playlist += string.Format("{0}\r\n", server);
			}

			await Task.Run(() => Console.WriteLine("Playlist created!"));

			MakeResponse(provider, playlist, BackendAction.Create, parameters, context);
		}

		public async void Download(int provider, string response, Dictionary<string, string> parameters, HttpListenerContext context)
		{
			var importDir = Path.Combine(Directory.GetCurrentDirectory(), "import");
			Directory.CreateDirectory(importDir);

			var ps = await db.SQLQuery<ulong>("SELECT file, ua, url FROM playlist_urls WHERE skip='0'");
			if (ps.Count != 0)
			{
				for (var i = ulong.MinValue; i < (ulong)ps.Count; i++)
				{
					var file = Path.Combine(importDir, await Functions.ReplaceAndFixNames(db, ps[i]["file"]));
					var dl = new HTTPClient(await Functions.ReplaceAndFixNames(db, ps[i]["url"], false));
					dl.UserAgent = ps[i]["ua"];

					File.WriteAllText(file, Encoding.UTF8.GetString(await dl.GetResponse()));
				}

				await Task.Run(() => Console.WriteLine("Playlists downloaded!"));
			}

			MakeResponse(provider, response, BackendAction.Download, parameters, context);
		}

		public async void Import(int provider, string response, Dictionary<string, string> parameters, HttpListenerContext context)
		{
			MakeResponse(provider, response, BackendAction.Import, parameters, context);
		}

		public async void Update(int provider, string response, Dictionary<string, string> parameters, HttpListenerContext context)
		{
			var channels = ChannelProvider.GetChannels();

			foreach (var c in channels)
				foreach (var s in c.Servers)
					if (!await TestChannelUrl(s.URL, await Functions.GetUseragentByID(db, s.UserAgent)))
						await Task.Run(() => db.SQLInsert(string.Format("DELETE FROM servers WHERE url='{0}'", s.URL)));

			MakeResponse(provider, string.Empty, BackendAction.Update, parameters, context);
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
