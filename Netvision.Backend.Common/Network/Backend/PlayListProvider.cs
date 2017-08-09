using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;

using Netvision.Backend.Provider;
using System.Threading.Tasks;
using System.IO;

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

			try
			{
				var req = WebRequest.CreateHttp(url);
				req.UserAgent = ua;

				using (var res = (HttpWebResponse)await req.GetResponseAsync())
					result = true;
			}
			catch (Exception ex)
			{
				await Task.Run(() => Console.WriteLine(ex.Message));
				result = false;
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
			var channels = (from c in ChannelProvider.Channels.Members.Values
							where c.Servers.Count != 0
							select c).ToList();

			for (var i = 0; i < channels.Count(); i++)
			{
				var server = channels.ElementAt(i).Servers[new Random().Next(0, (channels.ElementAt(i).Servers.Count - 1))].URL;
				var name = channels.ElementAt(i).Name;
				var logo = channels.ElementAt(i).Logo;

				if (string.IsNullOrEmpty(logo))
					logo = string.Empty;

				var epgid = channels.ElementAt(i).ID;
				var prov = await ChannelProvider.GetProviderNameByID(db, provider);
				playlist += string.Format("#EXTINF:-1 tvg-name=\"{2}\" group-title=\"{3}\" tvg-id=\"{0}\" tvg-logo=\"{1}\",{2}\r\n",
					epgid == 0 ? string.Empty : epgid.AsString(),
					!string.IsNullOrEmpty(logo) ? logo : string.Empty, name, prov);

				playlist += string.Format("{0}\r\n", server);
			}

			MakeResponse(provider, playlist, BackendAction.Create, parameters, context);
		}

		public async void Download(int provider, string response, Dictionary<string, string> parameters, HttpListenerContext context)
		{
			var ps = await db.SQLQuery<ulong>("SELECT * from playlist_urls WHERE skip='0'");
			if (ps.Count != 0)
			{
				for (var i = ulong.MinValue; i < (ulong)ps.Count; i++)
				{
					var ua = ps[i]["ua"];
					var url = await Functions.ReplaceAndFixNames(db, ps[i]["url"]);

					var file = string.Concat(Path.Combine(Directory.GetCurrentDirectory(),
						"import/", await Functions.ReplaceAndFixNames(db, ps[i]["file"])));

					using (var wc = new WebClient())
					{
						if (!string.IsNullOrEmpty(ua))
							wc.Headers.Add(HttpRequestHeader.UserAgent, ua);

						wc.DownloadFileCompleted += (sender, e) =>
						{
							Task.Run(() => Console.WriteLine("Playlist saved to: \"{0}\"", file));
						};

						wc.DownloadFileAsync(new Uri(url), file);
					}
				}
			}

			MakeResponse(provider, string.Empty, BackendAction.Download, parameters, context);
		}

		public async void Import(int provider, string response, Dictionary<string, string> parameters, HttpListenerContext context)
		{
		}

		public async void Update(int provider, string response, Dictionary<string, string> parameters, HttpListenerContext context)
		{
			var urls = (from c in ChannelProvider.Channels.Members.Values
						where c.Provider == provider
						where c.Servers.Count != 0
						select c.Servers.FirstOrDefault()).ToList();

			for (var i = 0; i < urls.Count; i++)
				if (!await TestChannelUrl(urls[i].URL, await Functions.GetUseragentByID(db, urls[i].UserAgent)))
					await Task.Run(() => db.SQLInsert(string.Format("DELETE FROM servers WHERE url='{0}'", urls[i])));

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
