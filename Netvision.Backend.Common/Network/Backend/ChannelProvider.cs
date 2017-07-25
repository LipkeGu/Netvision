using System;
using System.Collections.Generic;
using System.Net;
using Newtonsoft.Json;
using System.IO;
using System.Text;

namespace Netvision.Backend
{
	public class ChannelProvider
	{
		SQLDatabase db = new SQLDatabase("channels.db");

		public ChannelProvider(BackendHub backend)
		{
			var provider = db.SQLQuery(string.Format("SELECT id FROM providers WHERE id='{0}'", 1), "id");
			DownloadChannelList(provider);
		}

		void DownloadLogo(string logo, string provider)
		{
			var path = Path.Combine(Directory.GetCurrentDirectory(), "Logos");
			Directory.CreateDirectory(path);

			var logo_url = db.SQLQuery(string.Format("SELECT url FROM logo_lists WHERE provider='{0}'", provider), "url");
			if (string.IsNullOrEmpty(logo_url))
				return;

			using (var logo_wc = new WebClient())
			{
				logo_wc.DownloadFileCompleted += (sender, e) =>
				{ };

				var file = Path.Combine(path, logo);

				if (!File.Exists(file))
					logo_wc.DownloadFileAsync(new Uri(string.Format("{0}/{1}", logo_url, logo)), file);
			}
		}

		/// <summary>
		/// Add a Channel name and returns the id!
		/// </summary>
		/// <param name="name">Channel Name (Like ZDF)</param>
		/// <returns>The id of the last inserted Channel Name</returns>
		public string AddChannelName(ref SQLDatabase db, string name)
		{
			var id = string.Empty;

			if (db.Count("channel_names", "name", name) == 0)
				db.SQLInsert(string.Format("INSERT INTO channel_names (name) VALUES('{0}')", name));

			id = db.SQLQuery(string.Format("SELECT id FROM channel_names WHERE name='{0}'", name), "id");

			return id;
		}

		/// <summary>
		/// Add a Logo and returns the id!
		/// </summary>
		/// <param name="logo">file (Ex: logo.png)</param>
		/// <returns>The id of the last inserted Logo</returns>
		public string AddChannelLogo(string file, string provider)
		{
			var id = string.Empty;
			var list  = db.SQLQuery(string.Format("SELECT id FROM logo_lists WHERE provider='{0}'", provider),"id");

			if (db.Count("channel_logos", "file", file) == 0)
				db.SQLInsert(string.Format("INSERT INTO channel_logos (file, list) VALUES('{0}','{1}')", file, string.IsNullOrEmpty(list) ? "0" : list));

			id = db.SQLQuery(string.Format("SELECT id FROM channel_logos WHERE file='{0}'", file), "id");

			return id;
		}

		/// <summary>
		/// Add a EPGID and returns the id!
		/// </summary>
		/// <param name="epgid">The epgid of the Channel Ex: wdr.de )</param>
		/// <param name="provider">The id of the Provider Ex: "1")</param>
		/// <returns>The id of the last inserted EPGID</returns>
		/// 
		string AddChannelEPGID(string epgid, string provider)
		{
			var id = string.Empty;

			if (db.Count("epg_ids", "epgid", epgid) == 0)
				db.SQLInsert(string.Format("INSERT INTO epg_ids (epgid, provider) VALUES('{0}','{1}')", epgid, provider));

			id = db.SQLQuery(string.Format("SELECT id FROM epg_ids WHERE epgid='{0}' AND provider='{1}'", epgid, provider), "id");

			return id;
		}

		public static string GetLogoURLByID(ref SQLDatabase db, string list)
		{
			var x = string.Empty;

			x = db.SQLQuery(string.Format("SELECT url FROM logo_lists WHERE id='{0}'", list), "url");

			return x;
		}

		public static string GetLogoByID(ref SQLDatabase db, string id)
		{
			var url = string.Empty;

			var list = db.SQLQuery(string.Format("SELECT list FROM channel_logos WHERE id='{0}'", id), "list");
			url = GetLogoURLByID(ref db, list).Replace("--nolist--", string.Empty);
			var logo = db.SQLQuery(string.Format("SELECT file FROM channel_logos WHERE id='{0}'", id), "file").Replace("--nologo--", string.Empty);

			return (string.IsNullOrEmpty(url) || string.IsNullOrEmpty(logo)) ? string.Empty : string.Concat(url, logo); 
		}

		public static string GetNameByID(ref SQLDatabase db, string id)
		{
			return db.SQLQuery(string.Format("SELECT name FROM channel_names WHERE id='{0}'", id), "name");
		}

		public static string GetProviderNameByID(ref SQLDatabase db, string id)
		{
			return db.SQLQuery(string.Format("SELECT name FROM providers WHERE id='{0}'", id), "name");
		}

		public static string GetEPGIDrByID(ref SQLDatabase db, string id)
		{
			return db.SQLQuery(string.Format("SELECT epgid FROM epg_ids WHERE id='{0}'", id), "epgid");
		}

		string ReplaceAndFixNames(string input)
		{
			var output = input;

			if (output.Contains("|"))
				output = output.Split('|')[1].Trim();

			if (output.Contains(":"))
				output = output.Split(':')[1].Trim();

			if (output.EndsWith("HD"))
				output = output.Substring(0, output.Length - "HD".Length).Trim();

			if (output.EndsWith(" uuu"))
				output = output.Replace(" uuu", string.Empty).Trim();

			output = output.Replace("&nbsp;", string.Empty);
			output = output.Replace("&#135;", string.Empty);

			return output;
		}

		void CreateChannels(string provider)
		{
			Console.WriteLine("Adding Channels...");
			var json_input = string.Empty;

			using (var fs = new Filesystem(""))
				json_input = fs.ReadText("channels.json", Encoding.UTF8).Result;

			if (json_input.StartsWith("{"))
			{
				var ValueList = JsonConvert.DeserializeObject<Dictionary<string, object>>(json_input);
				
				foreach (var entry in ValueList)
				{
					var channel_entries = JsonConvert.DeserializeObject<List<vodafone_channel>>(string.Format("{0}", entry.Value));
					for (var i = 0; i < channel_entries.Count; i++)
					{
						var ch_nameid = AddChannelName(ref db, ReplaceAndFixNames(channel_entries[i].lname));

						var ch_logoid = AddChannelLogo(channel_entries[i].logo, provider);
						var ch_epgid = AddChannelEPGID(channel_entries[i].tvtvid, provider);

						if (db.Count("channels", "name", ch_nameid) == 0 && db.Count("channels", "logo", ch_logoid) == 0
							&& db.Count("channels", "epgid", ch_epgid) == 0)
						{
							InsertChannel(ch_nameid, ch_logoid, ch_epgid, provider);

							//DownloadLogo(channel_entries[i].logo, provider);
						}
					}
				}
			}

			Console.WriteLine("Done!");
			Directory.CreateDirectory(Path.Combine(Directory.GetCurrentDirectory(), "import"));

			var dirinfo = new DirectoryInfo(Path.Combine(Directory.GetCurrentDirectory(), "import"));
			foreach (var playlist in dirinfo.GetFiles("*.m3u8", SearchOption.TopDirectoryOnly))
			{
				Console.WriteLine("Importing {0}...", playlist.Name);

				using (var reader = new StreamReader(playlist.FullName, true))
				{
					var line = string.Empty;
					var channel = string.Empty;
					var status = 0;
					var url = string.Empty;
					var chan_nameid = string.Empty;

					while (!reader.EndOfStream)
					{
						line = reader.ReadLine();

						if (!string.IsNullOrEmpty(line) && line.Length > 10)
						{
							if (line.StartsWith("#EXTINF:-1"))
							{
								line = line.Split(',')[1].Trim();
								channel = ReplaceAndFixNames(line);

								var logo_id = AddChannelLogo("--nologo--", provider);
								var epgid = AddChannelEPGID("noepg.epg", "0");
								chan_nameid = AddChannelName(ref db, channel);

								if (db.Count("channels", "name", chan_nameid) == 0)
								{
									Console.WriteLine("Adding Channel: \"{0}\"", channel);
									InsertChannel(chan_nameid, logo_id, epgid, "0");
								}
							}
							else
							{
								url = line;
								if (url.StartsWith("http"))
								{
									try
									{
										var request = (HttpWebRequest)WebRequest.Create(url);
										using (var response = (HttpWebResponse)request.GetResponse())
										{
											switch (response.StatusCode)
											{
												case HttpStatusCode.OK:
													switch (response.ContentType.ToLowerInvariant())
													{
														case "application/x-mpegurl":
														case "application/vnd.apple.mpegurl":
															status = 1;
															break;
														case "video/mp2t":
															status = 1;
															break;
														default:
															status = 0;
															break;
													}
													break;
												default:
													status = 0;
													break;
											}
										}
									}
									catch (Exception ex)
									{
										Console.WriteLine("- Error: {0}", ex.Message);
										Console.WriteLine("- URL: {0}", url);
										Console.WriteLine("- Channel: {0}", channel);
										status = 0;
									}
								}
								else
								{
									if (line.StartsWith("rtmp://"))
									{
										url = line;
										status = 1;
									}
								}

								if (status != 0)
								{
									if (db.Count("channels", "name", chan_nameid) != 0 && !string.IsNullOrEmpty(chan_nameid))
									{
										var chan_id = db.SQLQuery(string.Format("SELECT id FROM channels WHERE name='{0}'", chan_nameid), "id");

										if (db.Count("servers", "url", url) == 0)
										{
											db.SQLInsert(string.Format("INSERT INTO servers (url, channel) VALUES('{0}','{1}')", url, chan_id));
											Console.WriteLine("Adding Server URL to Channel {0}: {1}", url, channel);
										}
									}
								}
							}
						}
					}
				}
			}

			Console.WriteLine("Importing complete!");
		}
		
		void InsertChannel(string name_id, string logo_id, string epg_id, string provider_id)
		{
			db.SQLInsert(string.Format("INSERT INTO channels (name, logo, provider, epgid) VALUES('{0}','{1}','{2}','{3}')",
				name_id, logo_id, provider_id, epg_id));
		}

		void DownloadChannelList(string provider)
		{
			var chan_wc = new WebClient();
			chan_wc.DownloadFileCompleted += (sender, e) =>
			{
				CreateChannels(provider);
				PlayListProvider.GeneratePlayList(db);
			};

			Console.WriteLine("Downloading Channel List for Provider: {0}",
				GetProviderNameByID(ref db, provider));

			chan_wc.DownloadFileAsync(new Uri(db.SQLQuery(string.Format(
				"SELECT url FROM channel_lists WHERE provider='{0}'",
				provider), "url")), "channels.json");
		}
	}
}
