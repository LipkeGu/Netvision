using System;
using System.Collections.Generic;
using System.Net;
using Newtonsoft.Json;
using System.IO;
using Netvision.Backend.Provider;
using System.Threading.Tasks;
using System.Text;
using System.Linq;

namespace Netvision.Backend
{
	public class ChannelProvider : IProvider
	{
		public delegate void ChannelProviderResponseEventHandler(object sender, ChannelProviderResponseEventArgs e);
		public event ChannelProviderResponseEventHandler ChannelProviderResponse;
		public class ChannelProviderResponseEventArgs : EventArgs
		{
			public BackendAction Action;
			public string Response;
			public int Provider;
			public HttpListenerContext context;
			public Dictionary<string, string> Parameters;
		}

		static SQLDatabase db = new SQLDatabase("channels.db");

		public static Provider<Channel> Channels =
			new Provider<Channel>(ref db, EntryType.Channel);

		async void ImportChannelsFromDataBase()
		{
			var chs = await db.SQLQuery<ulong>("SELECT * FROM channels");

			if (chs.Count != 0)
			{
				for (var i = ulong.MinValue; i < (ulong)chs.Count; i++)
				{
					var channel = new Channel();
					channel.Name = await GetChannelNameByID(db, int.Parse(chs[i]["name"]));
					channel.Logo = await GetLogoByID(db, int.Parse(chs[i]["logo"]), int.Parse(chs[i]["provider"]));
					channel.Provider = int.Parse(chs[i]["provider"]);
					channel.ChanDBID = int.Parse(chs[i]["id"]);
					channel.ID = int.Parse(chs[i]["epgid"]);
					channel.ChanNo = int.Parse(chs[i]["channo"]);

					var servers = await db.SQLQuery<ulong>(string.Format("SELECT * FROM servers WHERE channel='{0}'", chs[i]["id"]));
					if (servers.Count != 0)
						for (var i2 = ulong.MinValue; i2 < (ulong)servers.Count; i2++)
							channel.Servers.Add(new Server(servers[i2]["url"],
								int.Parse(servers[i2]["type"]), int.Parse(servers[i2]["ua"])));

					if (!Channels.Exist(channel.Name))
						Channels.Add(channel.Name, channel);
				}
			}
			else
				await Task.Run(() => Console.WriteLine("No Channels to load from Database!"));
		}

		public ChannelProvider(BackendHub backend)
		{
			Task.Run(() => ImportChannelsFromDataBase());

			backend.ChannelRequest += (sender, e) =>
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

		/// <summary>
		/// Add a Channel name and returns the id!
		/// </summary>
		/// <param name="name">Channel Name (Like ZDF)</param>
		/// <returns>The id of the last inserted Channel Name</returns>
		public async Task<int> AddChannelName(SQLDatabase db, string name)
		{
			if (await db.Count("channel_names", "name", name) == 0)
				await Task.Run(() => db.SQLInsert(string.Format("INSERT INTO channel_names (name) VALUES('{0}')", name)));

			return int.Parse(await db.SQLQuery(string.Format("SELECT id FROM channel_names WHERE name='{0}'", name), "id"));
		}

		/// <summary>
		/// Add a Logo and returns the id!
		/// </summary>
		/// <param name="logo">file (Ex: logo.png)</param>
		/// <returns>The id of the last inserted Logo</returns>
		public async Task<int> AddChannelLogo(string file, int provider)
		{
			var list = await db.SQLQuery(string.Format("SELECT id FROM logo_lists WHERE provider='{0}'", provider), "id");
			var x = string.IsNullOrEmpty(list) ? "0" : list;

			if (await db.Count("channel_logos", "file", file) == 0)
				await Task.Run(() => db.SQLInsert(string.Format("INSERT INTO channel_logos (file, list) VALUES('{0}','{1}')", file, x)));

			return int.Parse(await db.SQLQuery(string.Format("SELECT id FROM channel_logos WHERE file='{0}'", file), "id"));
		}

		/// <summary>
		/// Add a EPGID and returns the id!
		/// </summary>
		/// <param name="epgid">The epgid of the Channel Ex: wdr.de )</param>
		/// <param name="provider">The id of the Provider Ex: "1")</param>
		/// <returns>The id of the last inserted EPGID</returns>
		async Task<int> AddChannelEPGID<T>(T epgid, int provider)
		{
			try
			{
				if (await db.Count("epg_ids", "epgid", epgid) == 0)
					await Task.Run(() => db.SQLInsert(string.Format("INSERT INTO epg_ids (epgid, provider) VALUES('{0}','{1}')", epgid, provider)));

				return int.Parse(await db.SQLQuery(string.Format("SELECT id FROM epg_ids WHERE epgid='{0}' AND provider='{0}'", epgid, provider), "id"));
			}
			catch (Exception)
			{
				return 0;
			}
		}

		public async static Task<string> GetLogoURLByProvider(SQLDatabase db, int provider)
		{
			var s = await db.SQLQuery(string.Format("SELECT url FROM logo_lists WHERE provider='{0}'", provider), "url");
			if (s == "--nolist--")
				s = string.Empty;

			return s;
		}

		public async static Task<string> GetLogoByID(SQLDatabase db, int logo_id, int provider)
		{
			var logo = await db.SQLQuery(string.Format("SELECT file FROM channel_logos WHERE id='{0}'", logo_id), "file");
			if (logo == "--nologo--")
				logo = string.Empty;

			return logo;
		}

		public async static Task<string> GetChannelNameByID(SQLDatabase db, int id)
		{
			return await db.SQLQuery(string.Format("SELECT name FROM channel_names WHERE id='{0}'", id), "name");
		}

		public async static Task<string> GetProviderNameByID(SQLDatabase db, int id)
		{
			return await db.SQLQuery(string.Format("SELECT name FROM providers WHERE id='{0}'", id), "name");
		}

		public async static Task<int> GetProviderIDByName(SQLDatabase db, string name, int id = 0)
		{
			if (await db.Count("providers", "name", name) == 0)
				return id;

			return int.Parse(await db.SQLQuery(string.Format("SELECT id FROM providers WHERE name='{0}'", name), "id"));
		}

		public async static Task<string> GetEPGIDByID(SQLDatabase db, int id)
		{
			var x = await db.SQLQuery(string.Format("SELECT epgid FROM epg_ids WHERE id='{0}'", id), "epgid");
			return x = (x == "noepg.epg") ? string.Empty : x;
		}

		async Task<int> InsertChannel(int name_id, int logo_id, int epg_id, int provider_id, int type, int channo)
		{
			await Task.Run(() => db.SQLInsert(string.Format("INSERT INTO channels (name, logo, provider, epgid, type, channo) VALUES('{0}','{1}','{2}','{3}','{4}','{5}')",
					name_id, logo_id, provider_id, epg_id, type, channo)));

			return int.Parse(await db.SQLQuery(string.Format("SELECT id FROM channels WHERE name='{0}'", name_id), "id"));
		}

		public async void AddChannel(string name, string logo, int id,
			int provider, int chan_number, int type, Server server)
		{
			await Task.Run(() =>
			{
				lock (Channels)
				{
					if (!Channels.Exist(name))
					{
						var channel = new Channel();
						channel.Name = name;
						channel.Logo = logo;
						channel.ID = id;
						channel.Type = type;

						if (server != null)
							channel.Servers.Add(server);

						channel.Provider = provider;
						channel.ChanNo = chan_number;

						if (Channels.Add(channel.Name, channel))
							Console.WriteLine("Adding Channel: \"{0}\" (EPG ID: {1}; Channel Nr: {2})",
								channel.Name, channel.ID, channel.ChanNo);
					}
				}
			});
		}

		public async void Create(int provider, string response, Dictionary<string, string> parameters, HttpListenerContext context)
		{
			if (response.StartsWith("{"))
			{
				await Task.Run(() => Console.WriteLine("Adding Channels..."));

				response = response.Replace("\"ishd\"", "\"hd\"");
				response = response.Replace("\"lname\"", "\"Name\"");
				response = response.Replace("\"tvtvid\"", "id");
				response = response.Replace("\"channelNumber\"", "\"ChanNo\"");

				var ChannelList = JsonConvert.DeserializeObject<Dictionary<string, List<Channel>>>(response);

				foreach (var entry in ChannelList)
					for (var i = 0; i < entry.Value.Count; i++)
					{
						var chan_name = entry.Value[i].Name.FirstCharUpper(db).Result.Trim();

						AddChannel(chan_name, entry.Value[i].Logo, entry.Value[i].ID, provider,
							entry.Value[i].ChanNo, 0, null);
					}
			}

			MakeResponse(provider, string.Empty, BackendAction.Create, parameters, context);
		}

		public static IEnumerable<Channel> GetChannels()
		{
			return (from c in Channels.Members.Values where c.Servers.Count != 0 select c);
		}

		public static IEnumerable<int> GetChannelIDs()
		{
			return (from c in GetChannels() where c.ID != 0 select c.ID).OrderBy(c => c);
		}

		public async void Download(int provider, string response, Dictionary<string, string> parameters, HttpListenerContext context)
		{
			var prov = await GetProviderNameByID(db, provider);
			var res = string.Empty;
			using (var fs = new Filesystem())
			{
				var dir = string.Format("Download/channels/{0}", provider);
				Directory.CreateDirectory(dir);

				var f = fs.ResolvePath(Filesystem.Combine(dir, "channels.json"));
				if (!fs.Exists(f))
				{
					await Task.Run(() => Console.WriteLine("Downloading Channel List for Provider: {0}", prov));
					var url = await db.SQLQuery(string.Format("SELECT url FROM channel_lists WHERE provider='{0}'", provider), "url");
					var hc = new HTTPClient(url);

					res = Encoding.UTF8.GetString(await hc.GetResponse());

					await Task.Run(() => fs.Write(f, res));
				}
				else
				{
					await Task.Run(() => Console.WriteLine("Reading cached Channel List (\"{1}\") for Provider: {0}", prov, f));
					res = await fs.ReadText(f, Encoding.UTF8);
				}

				MakeResponse(provider, res, BackendAction.Download, parameters, context);
			}
		}

		public async void Import(int provider, string response, Dictionary<string, string> parameters, HttpListenerContext context)
		{
			var importDir = Path.Combine(Directory.GetCurrentDirectory(), "import");

			Directory.CreateDirectory(importDir);
			var dirinfo = new DirectoryInfo(importDir);

			foreach (var playlist in dirinfo.GetFiles("*.m3u8", SearchOption.TopDirectoryOnly))
			{
				await Task.Run(() => Console.WriteLine("Adding Channels from: {0}...", playlist.Name));

				using (var reader = new StreamReader(playlist.FullName, true))
				{
					var name = string.Empty;
					var status = 0;
					var url = string.Empty;

					var ua = await Functions.GetUseragentByID(db, 1);
					var uaid = await Functions.GetUseragentIDByName(db, ua);
					var type = 0;

					while (!reader.EndOfStream)
					{
						var line = await reader.ReadLineAsync();

						if (!string.IsNullOrEmpty(line) && line.Length > 10)
							if (line.StartsWith("#EXTINF:"))
							{
								line = await line.Split(',')[1].FirstCharUpper(db);
								name = line.Trim();
							}
							else
							{
								url = line;

								if (url.StartsWith("http"))
								{
									var hc = new HTTPClient(url);
									hc.UserAgent = ua;
									await hc.GetResponse();

									switch (hc.ContentType)
									{
										case "application/x-mpegurl":
										case "application/vnd.apple.mpegurl":
											status = 1;
											type = 1;
											break;
										case "video/mp2t":
											status = 1;
											type = 1;
											break;
										default:
											status = 0;
											type = 0;
											break;
									}
								}
								else
									if (url.StartsWith("rtmp") || url.StartsWith("udp"))
								{
									status = 1;
									type = 1;
								}
								else
								{
									status = 0;
									type = 0;
								}

								if (status != 0)
								{
									await Task.Run(() =>
									{
										lock (Channels)
											if (!Channels.Exist(name))
												AddChannel(name, GetLogoByID(db, AddChannelLogo("--nologo--", provider).Result, provider).Result,
													AddChannelEPGID("noepg.epg", provider).Result, GetProviderIDByName(db, "Custom", provider).Result, 0, type,
													new Server(url, type, uaid));
											else
												Channels.Members[name].Servers.Add(new Server(url, type, uaid));
									});
								}
							}
					}
				}
			}

			await Task.Run(() => Console.WriteLine("Playlists imported!"));
			MakeResponse(provider, string.Empty, BackendAction.Import, parameters, context);
		}

		public async void Update(int provider, string response, Dictionary<string, string> parameters, HttpListenerContext context)
		{
			var channels = GetChannels();
			foreach (var channel in channels)
				foreach (var server in channel.Servers)
				{
					if (server.URL.StartsWith("rtmp"))
						continue;

					try
					{
						var hc = new HTTPClient(server.URL);
						hc.UserAgent = await Functions.GetUseragentByID(db, server.UserAgent);
						await Task.Run(() => hc.GetResponse());

						if (hc.StatusCode != HttpStatusCode.OK &&
							hc.StatusCode != HttpStatusCode.Moved)
						{
							lock (Channels.Members)
								Channels.Members[channel.Name].Servers.Remove(server);

							await Task.Run(() => Console.WriteLine(
								"Removing URL: {0} (Status Code: {1})...", server.URL, hc.StatusCode));
						}
					}
					catch (Exception ex)
					{
						await Task.Run(() => Console.WriteLine("Url: {0}\r\nException: {1}", server.URL, ex));
					}
				}

			foreach (var item in channels)
			{
				var n = await item.Name.FirstCharUpper(db);
				var ch_nameid = await AddChannelName(db, n.Trim());
				var ch_logoid = await AddChannelLogo(item.Logo, item.Provider);
				var ch_epgid = await AddChannelEPGID(item.ID, item.Provider);

				if (await db.Count("channels", "name", ch_nameid) == 0 &&
					await db.Count("channels", "logo", ch_logoid) == 0 &&
					await db.Count("channels", "epgid", ch_epgid) == 0)
				{
					var chan_id = await InsertChannel(ch_nameid, ch_logoid,
						ch_epgid, item.Provider, item.Type, item.ChanNo);

					for (var i = 0; i < item.Servers.Count; i++)
						if (await db.Count("servers", "url", item.Servers[i].URL) == 0)
							await Task.Run(() => db.SQLInsert(string.Format("INSERT INTO servers (url, channel, type) VALUES ('{0}','{1}','{2}')",
								  item.Servers[i].URL, chan_id, item.Servers[i].Type)));
				}
			}
		}

		public void MakeResponse(int provider, string response, BackendAction action, Dictionary<string, string> parameters, HttpListenerContext context)
		{
			var evArgs = new ChannelProviderResponseEventArgs();
			evArgs.Provider = provider;
			evArgs.Action = action;
			evArgs.Response = response;
			evArgs.context = context;
			evArgs.Parameters = parameters;

			ChannelProviderResponse?.Invoke(this, evArgs);
		}

		public void Remove(int provider, string response, Dictionary<string, string> parameters, HttpListenerContext context)
		{
			throw new NotImplementedException();
		}

		public void Add(int provider, string response, Dictionary<string, string> parameters, HttpListenerContext context)
		{
			throw new NotImplementedException();
		}
	}
}
