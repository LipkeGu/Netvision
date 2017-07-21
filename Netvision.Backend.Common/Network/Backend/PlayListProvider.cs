using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.IO;

namespace Netvision.Backend
{
	public class PlayListProvider
	{
		public delegate void PlayListResponseEventHandler(object sender, PlayListResponseEventArgs e);
		public event PlayListResponseEventHandler PlayListResponse;
		public class PlayListResponseEventArgs : EventArgs
		{
			public string Response;
			public HttpListenerContext Context;
		}

		public PlayListProvider(BackendHub backend)
		{
			ImportPlayList();
			backend.PlayListRequest += (sender, e) =>
			{
				var evArgs = new PlayListResponseEventArgs();
				using (var db = new SQLDatabase("playlist.db"))
					evArgs.Response = GeneratePlayList(db);

				e.Context.Response.ContentType = "application/x-mpegURL";
				e.Context.Response.Headers.Add("Content-Disposition",
					"attachment; filename=playlist.m3u8");

				e.Context.Response.StatusCode = 200;
				e.Context.Response.StatusDescription = "OK";

				evArgs.Context = e.Context;
				PlayListResponse?.Invoke(this, evArgs);
			};
		}

		string GeneratePlayList(SQLDatabase db, bool heartbeat = false)
		{
			var result = db.SQLQuery<ulong>("SELECT * FROM channels");
			var playlist = "#EXTM3U\r\n";
			var server = string.Empty;

			for (var i = ulong.MinValue; i < (ulong)result.Count; i++)
			{
				server = GetURLForChannel(db, result[i]["id"]);
				if (string.IsNullOrEmpty(server))
					continue;

				playlist += string.Format("#EXTINF:-1 tvg-id=\"{0}\",{1}\r\n", result[i]["epgid"], result[i]["name"]);
				playlist += string.Format("{0}\r\n", server);

				Console.WriteLine("Channel added: {0}", result[i]["name"]);
			}

			Console.WriteLine("PlayList created!");
			return playlist;
		}

		void ImportPlayList()
		{
			var filename = "channels.m3u8";
			using (var db = new SQLDatabase("playlist.db"))
			{
				using (var reader = new StreamReader(filename))
				{
					var line = string.Empty;
					var channel = string.Empty;
					var chanid = string.Empty;
					var type = 0;
					var status = 0;
					var url = string.Empty;

					while (!reader.EndOfStream)
					{
						line = reader.ReadLine();

						if (!string.IsNullOrEmpty(line) && line.Length > 10)
						{
							if (line.StartsWith("#EXTINF:-1"))
							{
								line = line.Split(',')[1].Trim();

								if (line.Contains("|"))
									line = line.Split('|')[1].Trim();

								if (line.Contains(":"))
									line = line.Split(':')[1].Trim();

								if (line.EndsWith("HD"))
									line = line.Substring(0, line.Length - 2).Trim();

								if (line.EndsWith(" uuu"))
									line = line.Replace(" uuu", string.Empty).Trim();

								line = line.Replace("&nbsp;", string.Empty);
								line = line.Replace("&#135;", string.Empty);

								line = line.Replace("Das Erste", "ARD");
								line = line.Replace("Kabel Eins", "Kabel 1");
								line = line.Replace("Pro Sieben", "Pro 7");
								line = line.Replace("Prosieben", "Pro 7");

								line = line.Replace("RTL Television", "RTL");
								line = line.Replace("SUPER RTL", "Super RTL");
								line = line.Replace("SAT 1", "Sat 1");
								line = line.Replace("SAT1", "Sat 1");
								line = line.Replace("&amp;", "&");
								line = line.Replace("n-tvv", "NTV");
								line = line.Replace("n-tv", "NTV");
								line = line.Replace("N-TV", "NTV");
								line = line.Replace("N24", "N24");
								line = line.Replace("N-24", "N24");
								line = line.Replace("n24", "N24");
								line = line.Replace("n-24", "N24");


								channel = line;
							}
							else
							{
								if (line.StartsWith("http://") && !string.IsNullOrEmpty(channel))
								{
									url = line;

									var request = (HttpWebRequest)WebRequest.Create(url);
									var response = (HttpWebResponse)null;

									try
									{
										response = (HttpWebResponse)request.GetResponse();
										switch (response.StatusCode)
										{
											case HttpStatusCode.OK:
												switch (response.ContentType)
												{
													case "application/x-mpegurl":
													case "application/vnd.apple.mpegurl":
														status = 1;
														type = 0;
														break;
													case "video/mp2t":
														status = 1;
														type = 1;
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
									catch (Exception)
									{
										status = 0;
									}

									response?.Close();

									if (line.StartsWith("rtmp://"))
									{
										url = line;
										status = 1;
										type = 0;
									}

									/*
									if (db.Count("channels", "name", channel) == 0)
									{
										db.SQLInsert(string.Format("INSERT INTO channels (name) VALUES('{0}')", channel));
									}
									*/

									if (status != 0)
									{
										if (db.Count("channels", "name", channel) != 0)
										{
											chanid = db.SQLQuery(string.Format("SELECT id FROM channels WHERE name='{0}'", channel), "id");
											db.SQLInsert(string.Format("INSERT INTO servers (url, channel, type) VALUES('{0}','{1}','{2}')", url, chanid, type));
										}
										else
											Console.WriteLine("Ignoring WORKING URL ({0}) for channel: {1}", url, channel);
									}
								}
							}
						}
					}
				}
			}
		}

		bool TestChannelUrl(string url)
		{
			if (string.IsNullOrEmpty(url))
				return false;

			var result = false;
			var req = (HttpWebRequest)WebRequest.Create(url);
			using (var res = (HttpWebResponse)req.GetResponse())
			{
				
				switch (res.StatusCode)
				{
					case HttpStatusCode.OK:
						result = true;
						break;
					case HttpStatusCode.Forbidden:
					case HttpStatusCode.NotFound:
					default:
						result = false;
						break;
				}
			}

			return result;
		}

		string GetURLForChannel(SQLDatabase db, string id, bool heartbeat = false)
		{
			var urls = db.SQLQuery<ulong>(string.Format("SELECT * FROM servers WHERE channel='{0}'", id));
			var list = new List<string>();
			
			for (var i = ulong.MinValue; i < (ulong)urls.Count; i++)
			{
				list.Add((int.Parse(urls[i]["type"]) != 1) ? urls[i]["url"] :
					string.Format("plugin://plugin.video.f4mTester/?url={0}&amp;streamtype=TSDOWNLOADER&amp;maxbitrate=0&amp;Buffer=20971520",
					urls[i]["url"])
					);
			}

			return list.ElementAtOrDefault(new Random().Next(list.Count));
		}

		public void HeartBeat()
		{
			var updatePlayList = false;

			if (DateTime.Now.Hour == 2 || DateTime.Now.Hour == 8 ||
				DateTime.Now.Hour == 14 || DateTime.Now.Hour == 20)
			{
				if (DateTime.Now.Minute <= 3)
					updatePlayList = true;

				if (updatePlayList)
				{
					using (var db = new SQLDatabase("playlist.db"))
					{
						var urls = db.SQLQuery<ulong>("SELECT * FROM servers");
						for (var i = ulong.MinValue; i < (ulong)urls.Count; i++)
						{
							if (!TestChannelUrl(urls[i]["url"]))
								db.SQLInsert(string.Format("DELETE FROM servers WHERE url='{0}'",
									urls[i]["url"]));
						}
					}
				}
			}
		}
	}
}
