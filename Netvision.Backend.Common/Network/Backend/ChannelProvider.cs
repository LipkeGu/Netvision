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

		async Task<bool> ImportChannelsFromDataBase()
		{
			var result = false;

			var chs = db.SQLQuery<ulong>(string.Format("SELECT * FROM channels")).Result;

			if (chs.Count != 0)
			{
				for (var i = ulong.MinValue; i < (ulong)chs.Count; i++)
				{
					var channel = new Channel();
					channel.Name = GetChannelNameByID(db, int.Parse(chs[i]["name"])).Result;
					channel.Logo = GetLogoByID(db, int.Parse(chs[i]["logo"])).Result;
					channel.Provider = int.Parse(chs[i]["provider"]);
					channel.ChanDBID = int.Parse(chs[i]["id"]);
					channel.ID = int.Parse(chs[i]["epgid"]);
					channel.ChanNo = int.Parse(chs[i]["channo"]);

					var servers = db.SQLQuery<ulong>(string.Format("SELECT * FROM servers WHERE channel='{0}'", chs[i]["id"])).Result;
					if (servers.Count != 0)
						for (var i2 = ulong.MinValue; i2 < (ulong)servers.Count; i2++)
							channel.Servers.Add(new Server(servers[i2]["url"],
								int.Parse(servers[i2]["type"]), int.Parse(servers[i2]["ua"])));

					if (!Channels.Exist(channel.Name))
						Channels.Add(channel.Name, channel);
				}

				result = true;
			}
			else
				Task.Run(() => Console.WriteLine("No Channels to load from Database!"));

			return result;
		}

		public ChannelProvider(BackendHub backend)
		{
			backend.ChannelRequest += (sender, e) =>
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

		/// <summary>
		/// Add a Channel name and returns the id!
		/// </summary>
		/// <param name="name">Channel Name (Like ZDF)</param>
		/// <returns>The id of the last inserted Channel Name</returns>
		public async Task<int> AddChannelName(SQLDatabase db, string name)
		{
			if (db.Count("channel_names", "name", name).Result == 0)
				await Task.Run(() => db.SQLInsert(string.Format("INSERT INTO channel_names (name) VALUES('{0}')", name)));

			return int.Parse(db.SQLQuery(string.Format("SELECT id FROM channel_names WHERE name='{0}'", name), "id").Result);
		}

		/// <summary>
		/// Add a Logo and returns the id!
		/// </summary>
		/// <param name="logo">file (Ex: logo.png)</param>
		/// <returns>The id of the last inserted Logo</returns>
		public async Task<int> AddChannelLogo(string file, int id)
		{
			var list = db.SQLQuery(string.Format("SELECT id FROM logo_lists WHERE id='{0}'", id), "id").Result;
			var x = string.IsNullOrEmpty(list) ? "0" : list;

			if (db.Count("channel_logos", "file", file).Result == 0)
				await Task.Run(() => db.SQLInsert(string.Format("INSERT INTO channel_logos (file, list) VALUES('{0}','{1}')", file, x)));

			return int.Parse(db.SQLQuery(string.Format("SELECT id FROM channel_logos WHERE file='{0}'", file), "id").Result);
		}

		/// <summary>
		/// Add a EPGID and returns the id!
		/// </summary>
		/// <param name="epgid">The epgid of the Channel Ex: wdr.de )</param>
		/// <param name="provider">The id of the Provider Ex: "1")</param>
		/// <returns>The id of the last inserted EPGID</returns>
		async Task<int> AddChannelEPGID<T>(T epgid, int provider)
		{
			var result = 0;
			if (db.Count("epg_ids", "epgid", epgid).Result == 0)
				await Task.Run(() => db.SQLInsert(string.Format("INSERT INTO epg_ids (epgid, provider) VALUES('{0}','{1}')", epgid, provider)));

			if (db.Count("epg_ids", "epgid", epgid).Result != 0)
				if (string.Format("{0}", epgid) != "noepg.epg")
					result = int.Parse(db.SQLQuery(string.Format("SELECT id FROM epg_ids WHERE epgid='{0}' AND provider='{0}'", epgid, provider), "id").Result);

			return result;
		}

		public async static Task<string> GetLogoURLByID(SQLDatabase db, int id)
		{
			var s = db.SQLQuery(string.Format("SELECT url FROM logo_lists WHERE id='{0}'", id), "url").Result;
			if (s == "--nolist--")
				s = string.Empty;

			return s;
		}

		public async static Task<string> GetLogoByID(SQLDatabase db, int logo_id)
		{
			var logo = db.SQLQuery(string.Format("SELECT file FROM channel_logos WHERE id='{0}'", logo_id), "file").Result;
			if (logo == "--nologo--")
				logo = string.Empty;

			return logo;
		}

		public async static Task<string> GetChannelNameByID(SQLDatabase db, int id)
		{
			return db.SQLQuery(string.Format("SELECT name FROM channel_names WHERE id='{0}'", id), "name").Result;
		}

		public async static Task<string> GetProviderNameByID(SQLDatabase db, int id)
		{
			return db.SQLQuery(string.Format("SELECT name FROM providers WHERE id='{0}'", id), "name").Result;
		}

		public async static Task<int> GetProviderIDByName(SQLDatabase db, string name, int id = 0)
		{
			if (db.Count("providers", "name", name).Result == 0)
				return id;

			return int.Parse(db.SQLQuery(string.Format("SELECT id FROM providers WHERE name='{0}'", name), "id").Result);
		}

		public async static Task<string> GetEPGIDByID(SQLDatabase db, int id)
		{
			var x = db.SQLQuery(string.Format("SELECT epgid FROM epg_ids WHERE id='{0}'", id), "epgid").Result;
			return x = (x == "noepg.epg") ? string.Empty : x;
		}

		async Task<int> InsertChannel(int name_id, int logo_id, int epg_id, int provider_id, int type, int channo)
		{
			await Task.Run(() => db.SQLInsert(string.Format("INSERT INTO channels (name, logo, provider, epgid, type, channo) VALUES('{0}','{1}','{2}','{3}','{4}','{5}')",
					name_id, logo_id, provider_id, epg_id, type, channo)));

			return int.Parse(db.SQLQuery(string.Format("SELECT id FROM channels WHERE name='{0}'", name_id), "id").Result);
		}

		public async Task<bool> AddChannel(string name, string logo, int id,
			int provider, int chan_number, int type, Server server)
		{
			var result = false;
			await Task.Run(() =>
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
						result = true;
				}
			});

			return result;
		}

		public async void Create(int provider, string response, Dictionary<string, string> parameters, HttpListenerContext context)
		{
			if (response.StartsWith("{"))
			{
				Task.Run(() => Console.WriteLine("Adding Channels...")).ContinueWith(c =>
				{

					response = response.Replace("\"ishd\"", "\"hd\"");
					response = response.Replace("\"lname\"", "\"Name\"");
					response = response.Replace("\"tvtvid\"", "id");
					response = response.Replace("\"channelNumber\"", "\"ChanNo\"");

					var ChannelList = JsonConvert.DeserializeObject<Dictionary<string, List<Channel>>>(response);
					if (ChannelList.Count != 0)
						foreach (var entry in ChannelList)
							for (var i = 0; i < entry.Value.Count; i++)
							{
								var chan_name = entry.Value[i].Name.FirstCharUpper(db).Result.Trim();

								AddChannel(chan_name, entry.Value[i].Logo, entry.Value[i].ID, provider,
									entry.Value[i].ChanNo, 0, null);
							}
				});
			}

			Task.Run(() => MakeResponse(provider, string.Empty, BackendAction.Create, parameters, context));
		}

		public static IEnumerable<Channel> GetChannels()
		{
			var x = (from c in Channels.Members.Values
					 where c.Servers.Count != 0
					 select c).OrderBy(c => c.Name);
			return x;
		}

		public static IEnumerable<int> GetChannelIDs()
		{
			return (from c in GetChannels()
					where c.ID != 0
					select c.ID).OrderBy(c => c);
		}

		public async void Download(int provider, string response, Dictionary<string, string> parameters, HttpListenerContext context)
		{
			var prov = GetProviderNameByID(db, provider);
			var res = string.Empty;

			using (var fs = new Filesystem())
			{
				if (parameters.ContainsKey("chanlist"))
				{
					var dir = string.Format("Download/channels/{0}", parameters["chanlist"]);
					Directory.CreateDirectory(dir);

					Task.Run(() => Console.WriteLine("Downloading Channel List for Provider: {0}", prov.Result));
					var lists = db.SQLQuery<uint>(string.Format("SELECT * FROM channel_lists WHERE id='{0}' AND id != '0'", parameters["chanlist"])).Result;
					if (lists.Count != 0)
					{
						for (var i = uint.MinValue; i < lists.Count; i++)
						{
							var hc = new HTTPClient(Functions.ReplaceAndFixNames(db, lists[i]["url"], false).Result);
							hc.HTTPClientError += (sender, e) => Console.WriteLine(e.Message);
							hc.HTTPClientResponse += (sender, e) =>
							{
								hc.GetResponse(prov.Result);
								if (e.Data.Length != 0)
								{
									res = e.Data;
									fs.Write(fs.ResolvePath(Filesystem.Combine(dir,
										Functions.ReplaceAndFixNames(db, lists[i]["file"]).Result)), res);
								}
							};
						}
					}
				}
			}

			Task.Run(() => MakeResponse(provider, res, BackendAction.Download, parameters, context));
		}

		public async void Import(int provider, string response, Dictionary<string, string> parameters, HttpListenerContext context)
		{
			Task.Run(() => ImportChannelsFromDataBase());
			var importDir = Path.Combine(Directory.GetCurrentDirectory(),
				string.Format("Download/playlist/{0}", provider));

			Directory.CreateDirectory(importDir);
			var dirinfo = new DirectoryInfo(importDir);

			foreach (var playlist in dirinfo.GetFiles("*.m3u8", SearchOption.AllDirectories))
			{
				Console.WriteLine("Adding Channels from: {0}...", playlist.Name);
				using (var reader = new StreamReader(playlist.FullName, true))
				{
					var name = string.Empty;
					var status = 0;
					var logo = "--nologo--";
					var url = string.Empty;
					var ua = Functions.GetUseragentByID(db, 1);
					var uaid = Functions.GetUseragentIDByName(db, ua.Result);
					var type = 0;

					while (!reader.EndOfStream)
					{
						var line = reader.ReadLineAsync().Result;

						if (!string.IsNullOrEmpty(line))
							if (line.Length > 10)
								if (line.StartsWith("#EXTINF:"))
								{
									if (line.Contains(","))
									{
										var parts = line.Split(',');
										if (parts.Length > 1)
											name = Functions.ReplaceAndFixNames(db, parts[1].FirstCharUpper(db).Result.Trim()).Result;

										if (parts[0].Contains("tvg-logo"))
										{
											var start = parts[0].IndexOf("tvg-logo=\"") + "tvg-logo=\"".Length;
											var end = parts[0].IndexOf("\"", start);

											var tmp = parts[0].Substring(start, (end - start)).Split('/');
											logo = tmp[tmp.Length - 1];

										}
									}
								}
								else
								{
									if (line.StartsWith("http") || line.StartsWith("rtmp") || line.StartsWith("udp"))
									{
										url = line;

										if (url.StartsWith("http"))
										{
											var hc = new HTTPClient(url, ua.Result);
											hc.HTTPClientError += (sender, e) =>
											{
												Console.WriteLine(e.Message);
												status = 0;
											};

											hc.GetResponse(name);
											hc.HTTPClientResponse += (sender, e) =>
											{
												switch (hc.ContentType)
												{
													case "application/x-mpegurl":
													case "application/vnd.apple.mpegurl":
														status = 1;
														type = 1;
														break;
													case "video/mp2t":
														status = 1;
														type = 2;
														break;
													default:
														status = 0;
														type = 0;
														break;
												}

												if (status != 0)
												{
													Task.Run(() =>
													{
														_addchannel(url, name, type, uaid.Result, provider,
															logo, int.Parse(parameters["logolist"]));
													});
												}
											};
										}
										else
										{
											if (url.StartsWith("rtmp") || url.StartsWith("udp"))
											{
												status = 1;
												type = 1;
											}
											else
											{
												status = 1;
												type = 0;
											}
										}

										if (status != 0)
										{
											await Task.Run(() =>
											{
												_addchannel(url, name, type, uaid.Result, provider, logo, int.Parse(parameters["logolist"]));
											});
										}
									}
								}
					}
				}
			}

			Task.Run(() => MakeResponse(provider, string.Empty, BackendAction.Import, parameters, context));
		}

		async void _addchannel(string url, string name, int type, int uaid, int provider, string logo, int logolist)
		{
			if (!string.IsNullOrEmpty(name))
				if (!Channels.Exist(name))
				{
					var channel_logoid = AddChannelLogo(logo, logolist);
					var epgid = AddChannelEPGID("noepg.epg", provider);

					var c = AddChannel(name, GetLogoByID(db, channel_logoid.Result).Result,
						epgid.Result, provider, 0, type, new Server(url, type, uaid)).Result;

					Console.WriteLine("Adding Channel: {0}...", name);
				}
				else
					if (Channels.Members.ContainsKey(name))
					Channels.Members[name].Servers.Add(new Server(url, type, uaid));
		}

		public async void Update(int provider, string response, Dictionary<string, string> parameters, HttpListenerContext context)
		{
			var channels = GetChannels();
			foreach (var channel in channels)
				foreach (var server in channel.Servers)
				{
					if (server.URL.StartsWith("rtmp"))
						continue;

					var hc = new HTTPClient(server.URL);
					hc.UserAgent = Functions.GetUseragentByID(db, server.UserAgent).Result;
					await Task.Run(() => hc.GetResponse(channel.Name)).ContinueWith(c =>
					{
						if (hc.StatusCode != HttpStatusCode.OK &&
												hc.StatusCode != HttpStatusCode.Moved)
						{
							lock (Channels.Members)
								Channels.Members[channel.Name].Servers.Remove(server);

							Task.Run(() => Console.WriteLine("Removing URL: {0} (Status Code: {1})...", server.URL, hc.StatusCode));
						}
					});
				}

			foreach (var item in channels)
			{
				var n = item.Name.FirstCharUpper(db).Result;
				var ch_nameid = AddChannelName(db, n.Trim()).Result;
				var ch_logoid = AddChannelLogo(item.Logo, item.Provider).Result;
				var ch_epgid = AddChannelEPGID(item.ID, item.Provider).Result;

				if (db.Count("channels", "name", ch_nameid).Result == 0 &&
					db.Count("channels", "logo", ch_logoid).Result == 0 &&
					db.Count("channels", "epgid", ch_epgid).Result == 0)
				{
					var chan_id = InsertChannel(ch_nameid, ch_logoid,
						ch_epgid, item.Provider, item.Type, item.ChanNo);

					for (var i = 0; i < item.Servers.Count; i++)
						if (db.Count("servers", "url", item.Servers[i].URL).Result == 0)
							await Task.Run(() => db.SQLInsert(string.Format("INSERT INTO servers (url, channel, type) VALUES ('{0}','{1}','{2}')",
								  item.Servers[i].URL, chan_id.Result, item.Servers[i].Type)));
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
