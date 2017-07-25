using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Net;
using System.IO;
using System.Text;

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
			backend.PlayListRequest += (sender, e) =>
			{
				var evArgs = new PlayListResponseEventArgs();

				using (var fs = new Filesystem(Directory.GetCurrentDirectory()))
					evArgs.Response = fs.ReadText("playlist.m3u8", Encoding.UTF8).Result;

				e.Context.Response.ContentType = "application/x-mpegURL";
				e.Context.Response.Headers.Add("Content-Disposition",
					"attachment; filename=playlist.m3u8");

				e.Context.Response.StatusCode = 200;
				e.Context.Response.StatusDescription = "OK";

				evArgs.Context = e.Context;
				PlayListResponse?.Invoke(this, evArgs);
			};
		}

		public static Dictionary<T, NameValueCollection> GetServerss<T>(ref SQLDatabase db, string id)
		{
			return db.SQLQuery<T>(string.Format("SELECT * FROM servers WHERE channel='{0}'", id));
		}

		public static Dictionary<T, NameValueCollection> GetChannels<T>(ref SQLDatabase db)
		{
			return db.SQLQuery<T>("SELECT * from channels");
		}

		public static void GeneratePlayList(SQLDatabase db, bool heartbeat = false)
		{
			var channels = GetChannels<uint>(ref db);
			var playlist = "#EXTM3U\r\n";
			var server = string.Empty;
			Console.WriteLine("Generating Playlist");
			for (var i = uint.MinValue; i < channels.Count; i++)
			{
				server = GetURLForChannel(ref db, channels[i]["id"]);
				if (string.IsNullOrEmpty(server))
					continue;

				var logo = ChannelProvider.GetLogoByID(ref db, channels[i]["logo"]);
				playlist += string.Format("#EXTINF:-1 tvg-name=\"{2}\" tvg-id=\"{0}\" tvg-logo=\"{1}\",{2}\r\n",
					ChannelProvider.GetEPGIDrByID(ref db, channels[i]["epgid"]),
					logo, ChannelProvider.GetNameByID(ref db, channels[i]["name"]));

				playlist += string.Format("{0}\r\n", server);
			}

			using (var fstream = File.CreateText("playlist.m3u8"))
			{
				fstream.Write(playlist);
			}

			Console.WriteLine("Done!");
		}

		bool TestChannelUrl(string url)
		{
			if (string.IsNullOrEmpty(url))
				return false;

			if (url.StartsWith("rtmp"))
				return true;

			var result = false;
			try
			{
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
			}
			catch (Exception ex)
			{
				Console.WriteLine(ex.Message);
				return false;
			}


			return result;
		}

		static string GetURLForChannel(ref SQLDatabase db, string id, bool heartbeat = false)
		{
			var list = new List<string>();
			var urls = GetServerss<ulong>(ref db, id);

			for (var i = ulong.MinValue; i < (ulong)urls.Count; i++)
			{
				if (!string.IsNullOrEmpty(urls[i]["url"]))
					list.Add(urls[i]["url"]);
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
					using (var db = new SQLDatabase("channels.db"))
					{
						var urls = db.SQLQuery<ulong>("SELECT * FROM servers");
						for (var i = ulong.MinValue; i < (ulong)urls.Count; i++)
						{
							Console.WriteLine("Testting URL: {0}", urls[i]["url"]);
							if (!TestChannelUrl(urls[i]["url"]))
								db.SQLInsert(string.Format("DELETE FROM servers WHERE url='{0}'",
									urls[i]["url"]));
						}

						GeneratePlayList(db, true);
					}
				}
			}
		}
	}
}
