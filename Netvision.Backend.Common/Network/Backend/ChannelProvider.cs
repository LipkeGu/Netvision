using System;
using System.Collections.Generic;
using System.Net;
using Newtonsoft.Json;
using System.IO;
using System.Text;

using Netvision.Backend.Providers;

namespace Netvision.Backend
{
    public class ChannelProvider
    {
        SQLDatabase db = new SQLDatabase("channels.db");

        public static Provider<Channel> Channels = 
            new Provider<Channel>(EntryType.Channel);

        public ChannelProvider(BackendHub backend)
        {
            var chs = db.SQLQuery<ulong>("SELECT * FROM channels");
            if (chs.Count != 0)
            {
                for (var i = ulong.MinValue; i < (ulong)chs.Count; i++)
                {
                    var channel = new Channel();
                    channel.Name = GetChannelNameByID(ref db, int.Parse(chs[i]["id"]));
                    channel.Logo = GetLogoByID(db, int.Parse(chs[i]["logo"]), int.Parse(chs[i]["provider"]));
                    channel.Provider = int.Parse(chs[i]["provider"]);
                    channel.ID = int.Parse(chs[i]["epgid"]);

                    channel.ChanNo = int.Parse(chs[i]["channo"]);

                    var servers = db.SQLQuery<ulong>(string.Format("SELECT * from servers channel='{0}}'", chs[i]["id"]));
                    for (var i2 = ulong.MinValue; i2 < (ulong)chs.Count; i++)
                    {
                        channel.Servers.Add(new Server(servers[i2]["url"], 
                                int.Parse(servers[i2]["type"])));
                    }

                    Channels.Add(channel.Name.Trim(), channel);
                }
            }
            else
            {
                Console.WriteLine("No Channels to load from Database!");
            }

            var provider = int.Parse(db.SQLQuery(string.Format("SELECT id FROM providers WHERE id='{0}'", 1), "id"));
            DownloadChannelList(provider);
        }

        /// <summary>
        /// Add a Channel name and returns the id!
        /// </summary>
        /// <param name="name">Channel Name (Like ZDF)</param>
        /// <returns>The id of the last inserted Channel Name</returns>
        public int AddChannelName(ref SQLDatabase db, string name)
        {
            if (db.Count("channel_names", "name", name) == 0)
                db.SQLInsert(string.Format("INSERT INTO channel_names (name) VALUES('{0}')", name));

            return int.Parse(db.SQLQuery(string.Format("SELECT id FROM channel_names WHERE name='{0}'", name), "id"));
        }

        /// <summary>
        /// Add a Logo and returns the id!
        /// </summary>
        /// <param name="logo">file (Ex: logo.png)</param>
        /// <returns>The id of the last inserted Logo</returns>
        public int AddChannelLogo(string file, int provider)
        {
            var list = db.SQLQuery(string.Format("SELECT id FROM logo_lists WHERE provider='{0}'", provider), "id");

            if (db.Count("channel_logos", "file", file) == 0)
                db.SQLInsert(string.Format("INSERT INTO channel_logos (file, list) VALUES('{0}','{1}')",
                        file, string.IsNullOrEmpty(list) ? "0" : list));

            return int.Parse(db.SQLQuery(string.Format("SELECT id FROM channel_logos WHERE file='{0}'", file), "id"));
        }

        /// <summary>
        /// Add a EPGID and returns the id!
        /// </summary>
        /// <param name="epgid">The epgid of the Channel Ex: wdr.de )</param>
        /// <param name="provider">The id of the Provider Ex: "1")</param>
        /// <returns>The id of the last inserted EPGID</returns>
        int AddChannelEPGID<T>(T epgid, int provider)
        {
            try
            {
                
                if (db.Count("epg_ids", "epgid", epgid) == 0)
                    db.SQLInsert(string.Format("INSERT INTO epg_ids (epgid, provider) VALUES('{0}','{1}')", epgid, provider));
            
                return int.Parse(db.SQLQuery(string.Format("SELECT id FROM epg_ids WHERE epgid='{0}' AND provider='{0}'", epgid, provider), "id"));
            }
            catch (Exception)
            {
                return 0;
            }
        }

        public static string GetLogoURLByProvider(SQLDatabase db, int provider)
        {
            return db.SQLQuery(string.Format("SELECT url FROM logo_lists WHERE provider='{0}'", provider), "url");
        }

        public static string GetLogoByID(SQLDatabase db, int logo_id, int provider)
        {
            var url = GetLogoURLByProvider(db, provider).Replace("--nolist--", string.Empty);
            var logo = db.SQLQuery(string.Format("SELECT file FROM channel_logos WHERE id='{0}'", logo_id), "file")
                .Replace("--nologo--", string.Empty);

            return (string.IsNullOrEmpty(url) || string.IsNullOrEmpty(logo)) ? string.Empty : string.Concat(url, logo); 
        }

        public static string GetChannelNameByID(ref SQLDatabase db, int id)
        {
            return db.SQLQuery(string.Format("SELECT name FROM channel_names WHERE id='{0}'", id), "name");
        }

        public static string GetProviderNameByID(SQLDatabase db, int id)
        {
            return db.SQLQuery(string.Format("SELECT name FROM providers WHERE id='{0}'", id), "name");
        }

        public static string GetEPGIDByID(ref SQLDatabase db, int id)
        {
            return db.SQLQuery(string.Format("SELECT epgid FROM epg_ids WHERE id='{0}'", id), "epgid");
        }

        public void HeartBeat()
        {
            var commitChannels = false;

            if (DateTime.Now.Hour == 4 || DateTime.Now.Hour == 10 ||
                DateTime.Now.Hour == 16 || DateTime.Now.Hour == 22)
            {
                if (DateTime.Now.Minute < 3)
                    commitChannels = true;

                if (!commitChannels)
                    return;
                
                lock (Channels.Members)
                {
                    foreach (var item in Channels.Members.Values)
                    {
                        var ch_nameid = AddChannelName(ref db, item.Name.FirstCharUpper());
                        var ch_logoid = AddChannelLogo(item.Logo, item.Provider);
                        var ch_epgid = AddChannelEPGID(item.ID, item.Provider);

                        if (db.Count("channels", "name", ch_nameid) == 0 &&
                            db.Count("channels", "logo", ch_logoid) == 0 &&
                            db.Count("channels", "epgid", ch_epgid) == 0)
                        {
                            var chan_id = InsertChannel(ch_nameid, ch_logoid, ch_epgid, item.Provider, item.Type, item.ChanNo);

                            for (var i = 0; i < item.Servers.Count; i++)
                            {
                                db.SQLInsert(string.Format("INSERT INTO servers (url, channel, type) VALUES ('{0}','{1}','{2}')",
                                        item.Servers[i].URL, chan_id, item.Servers[i].Type));   
                            }
                        }
                    }
                }
            }
        }

        void CreateChannels(int provider, string input)
        {
            if (input.StartsWith("{"))
            {
                Console.WriteLine("Adding Channels...");

                input = input.Replace("\"ishd\"", "\"hd\"");
                input = input.Replace("\"lname\"", "\"Name\"");
                input = input.Replace("\"tvtvid\"", "id");
                input = input.Replace("\"channelNumber\"", "\"ChanNo\"");

                var ChannelList = JsonConvert.DeserializeObject<Dictionary<string, List<Channel>>>(input);
				
                foreach (var entry in ChannelList)
                {
                    for (var i = 0; i < entry.Value.Count; i++)
                    {
                        var chan_name = entry.Value[i].Name.FirstCharUpper();
                        if (!Channels.Exist(chan_name))
                        {
                            var channel = new Channel();
                            channel.Name = chan_name;
                            channel.Logo = entry.Value[i].Logo;
                            channel.ID = entry.Value[i].ID;
                            channel.Provider = provider;
                            channel.ChanNo = entry.Value[i].ChanNo;

                            Console.WriteLine("Adding Channel: \"{0}\" (EPG ID: {1}; Channel Nr: {2})",
                                channel.Name, channel.ID, channel.ChanNo);
                            
                            Channels.Add(channel.Name, channel);
                        }
                    }
                }

                Console.WriteLine("Done!");
            }

            Directory.CreateDirectory(Path.Combine(Directory.GetCurrentDirectory(), "import"));

            var dirinfo = new DirectoryInfo(Path.Combine(Directory.GetCurrentDirectory(), "import"));
            foreach (var playlist in dirinfo.GetFiles("*.m3u8", SearchOption.TopDirectoryOnly))
            {
                Console.WriteLine("Adding Channels from: {0}...", playlist.Name);

                using (var reader = new StreamReader(playlist.FullName, true))
                {
                    var line = string.Empty;
                    var name = string.Empty;
                    var status = 0;
                    var url = string.Empty;
                    var type = 1;

                    while (!reader.EndOfStream)
                    {
                        line = reader.ReadLine();

                        if (!string.IsNullOrEmpty(line) && line.Length > 10)
                        {
                            if (line.StartsWith("#EXTINF:"))
                            {
                                line = line.Split(',')[1].Trim().FirstCharUpper();
                                name = line;
                            }
                            else
                            {
                                url = line;
                                if (url.StartsWith("http"))
                                {
                                    try
                                    {
                                        var request = (HttpWebRequest)WebRequest.Create(url);
                                        request.UserAgent = "VLC/2.2.2 LibVLC/2.2.2";
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
                                                            type = 1;
                                                            break;
                                                        case "video/mp2t":
                                                            status = 1;
                                                            type = 2;
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
                                        Console.WriteLine("- Channel: {0}", name);
                                        status = 0;
                                    }
                                }
                                else
                                {
                                    if (line.StartsWith("rtmp://"))
                                    {
                                        url = line;
                                        status = 1;
                                        type = 1;
                                    }
                                }

                                if (status != 0)
                                {
                                    var channel = new Channel();
                                    channel.Name = name;
                                    channel.Logo = GetLogoByID(db, AddChannelLogo("--nologo--", provider), provider);
                                    channel.ID = AddChannelEPGID<string>("noepg.epg", provider);
                                    channel.Provider = provider;
                                    channel.Type = type;
                                    channel.Servers.Add(new Server(url, type));
                                    channel.ChanNo = 0;

                                    if (!Channels.Exist(name))
                                    {
                                        Console.WriteLine("Adding Channel: \"{0}\" (EPG ID: {1}; Channel Nr: {2})",
                                            channel.Name, channel.ID, channel.ChanNo);

                                        Channels.Add(channel.Name, channel);
                                    }
                                    else
                                        Channels.Members[channel.Name].Servers.Add(new Server(url, type));
                                }
                            }
                        }
                    }
                }
            }

            Console.WriteLine("Importing complete!");
        }

        int InsertChannel(int name_id, int logo_id, int epg_id, int provider_id, int type, int channo)
        {
            db.SQLInsert(string.Format("INSERT INTO channels (name, logo, provider, epgid, type, channo) VALUES('{0}','{1}','{2}','{3}','{4}','{5}')",
                    name_id, logo_id, provider_id, epg_id, type, channo));

            return int.Parse(db.SQLQuery(string.Format("SELECT id FROM channels WHERE name='{0}'", name_id), "id"));
        }

        void DownloadChannelList(int provider)
        {
            using (var chan_wc = new WebClient())
            {
                var url = string.Empty;

                chan_wc.DownloadStringCompleted += (sender, e) =>
                {
                    var parameters = new Dictionary<string, string>();
                    CreateChannels(provider, e.Result);

                    PlayListProvider.GeneratePlayList(ref parameters, "1");
                };

                Console.WriteLine("Downloading Channel List for Provider: {0}", GetProviderNameByID(db, provider));

                url = db.SQLQuery(string.Format("SELECT url FROM channel_lists WHERE provider='{0}'", provider), "url");

                chan_wc.DownloadStringAsync(new Uri(url));
            }
        }
    }
}
