using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Net;
using System.IO;
using System.Text;

using Netvision.Backend.Providers;

namespace Netvision.Backend
{
    public class PlayListProvider
    {
        public delegate void PlayListResponseEventHandler(object sender,PlayListResponseEventArgs e);

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
                using (var fs = new Filesystem(Directory.GetCurrentDirectory(), 4096))
                {
                    if (e.Parameters.ContainsKey("listtype"))
                    {
                        var listtype = e.Parameters.ContainsKey("listtype") ? e.Parameters["listtype"] : "1";
                        using (var db = new SQLDatabase("channels.db"))
                        {
                            evArgs.Response = GeneratePlayList(ref e.Parameters, listtype, true);

                            e.Context.Response.ContentType = "application/x-mpegURL";
                            e.Context.Response.Headers.Add("Content-Disposition",
                                "attachment; filename=playlist.m3u8");

                            e.Context.Response.StatusCode = 200;
                            e.Context.Response.StatusDescription = "OK";
                        }
                    }
                    else
                    {
                        if (fs.Exists("playlist.m3u8"))
                        {
                            evArgs.Response = fs.ReadText("playlist.m3u8", Encoding.UTF8).Result;

                            e.Context.Response.ContentType = "application/x-mpegURL";
                            e.Context.Response.Headers.Add("Content-Disposition",
                                "attachment; filename=playlist.m3u8");

                            e.Context.Response.StatusCode = 200;
                            e.Context.Response.StatusDescription = "OK";
                        }
                        else
                        {
                            e.Context.Response.StatusCode = 503;
                            e.Context.Response.StatusDescription = "Service Unavailable";
                        }
                    }
                }

                evArgs.Context = e.Context;
                PlayListResponse?.Invoke(this, evArgs);
            };
        }

        static List<Channel> GetChannels(string type)
        {
            return (from c in ChannelProvider.Channels.Members
                             where c.Value.Servers.Count != 0
                             select c.Value).ToList<Channel>();
        }

        public static string GeneratePlayList(ref Dictionary<string,string> parameters, string type, bool heartbeat = false)
        {
            var playlist = "#EXTM3U\r\n";       
            Console.WriteLine("Generating Playlist...");

            var channels = GetChannels(type);
            using (var db = new SQLDatabase("channels.db"))
            {    
                for (var i = 0; i < channels.Count; i++)
                {
                    var server = channels[i].Servers.ElementAtOrDefault(new Random().Next(channels[i].Servers.Count)).URL;
                   
                    var name = channels[i].Name;

                    var logo_url = ChannelProvider.GetLogoURLByProvider(db, channels[i].Provider);
                    var logo = channels[i].Logo;
                       
                    if (string.IsNullOrEmpty(logo_url))
                        logo = string.Empty;
                    
                    var epgid = channels[i].ID;
                    var provider = ChannelProvider.GetProviderNameByID(db, channels[i].Provider);

                    playlist += string.Format("#EXTINF:-1 tvg-name=\"{2}\" tvg-group=\"{3}\" tvg-id=\"{0}\" tvg-logo=\"{1}\",{2}\r\n",
                        epgid == 0 ? string.Empty : epgid.AsString(), 
                        !string.IsNullOrEmpty(logo) ? string.Concat(logo_url, logo) : string.Empty,
                        name, provider);
                    
                    server = server.Replace("[#BUFFER#]", parameters.ContainsKey("buffer") ? parameters["buffer"] : "16000000");
                    playlist += string.Format("{0}\r\n", server);
                }
            }
            if (type != "2")
            {
                using (var fstream = File.CreateText("playlist.m3u8"))
                {
                    fstream.Write(playlist);
                }

                Console.WriteLine("Done!");
            }

            return playlist;
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
                req.UserAgent = "VLC/2.2.2 LibVLC/2.2.2";
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

        static string GetURLForChannel(ref SQLDatabase db, int id, string type, bool heartbeat = false)
        {
            var list = new List<string>();
            var urls = (from s in ChannelProvider.Channels.Members.Values
                                 where s.ID == id
                                 select s.Servers).FirstOrDefault();
            
            var url = string.Empty;

            for (var i = 0; i < urls.Count; i++)
            {
                if (!string.IsNullOrEmpty(urls[i].URL))
                {
                    if (urls[i].Type == 2)
                        url = string.Format("plugin://plugin.video.f4mTester/?url={0}&amp;streamtype=TSDOWNLOADER&amp;maxbitrate=0&amp;Buffer=[#BUFFER#]", urls[i].URL);
                    else
                        url = urls[i].URL;

                    list.Add(url);
                }
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
                        var urls = db.SQLQuery<ulong>("SELECT * from servers");
                        for (var i = ulong.MinValue; i < (ulong)urls.Count; i++)
                        {
                            if (!TestChannelUrl(urls[i]["url"]))
                                db.SQLInsert(string.Format("DELETE FROM servers WHERE url='{0}'", urls[i]["url"]));
                        }
                    }
                }
            }
        }
    }
}
