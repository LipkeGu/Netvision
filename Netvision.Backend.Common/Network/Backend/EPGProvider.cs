using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net;

using System.Threading.Tasks;
using System.Xml;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.IO;

namespace Netvision.Backend
{
	public class EPGProvider
	{
		public delegate void EPGResponseEventHandler(object sender, EPGResponseEventArgs e);
		public event EPGResponseEventHandler EPGResponse;
		public class EPGResponseEventArgs : EventArgs
		{
			public string Response;
			public HttpListenerContext Context;
		}

		SQLDatabase db;

		public EPGProvider(BackendHub backend)
		{
			db = new SQLDatabase("channels.db");
			backend.EPGRequest += (sender, e) =>
			{
				DownloadEPG("1");
			};
		}

		void DownloadEPG(string provider)
		{
			var epg_url = GetEPGProviderURL(provider);
			if (string.IsNullOrEmpty(epg_url))
			{
				Console.WriteLine("EPG download failed! Error: got empty URL!");

				return;
			}

			using (var epg_wc = new WebClient())
			{
				epg_wc.Encoding = Encoding.UTF8;

				epg_wc.DownloadStringCompleted += (sender, e) =>
				{
					Console.WriteLine("Download completed!");

					if (e.Result.StartsWith("{"))
					{
						var ValueList = JsonConvert.DeserializeObject<Dictionary<string, object>>(e.Result);
						foreach (var entry in ValueList)
						{
							var epg_entries = JsonConvert.DeserializeObject<List<Vodafone_epg>>(string.Format("{0}", entry.Value));
						}
					}

					return;
				};

				epg_url = epg_url.Replace("[#EPGID#]", "");
				epg_url = epg_url.Replace("[#YEAR#]", "");
				epg_url = epg_url.Replace("[#SDAY#]", "");
				epg_url = epg_url.Replace("[#EDAY#]", "");
				epg_url = epg_url.Replace("[#EPGID#]", "");
				epg_url = epg_url.Replace("[#EPGID#]", "");
				epg_url = epg_url.Replace("[#EPGID#]", "");
				epg_url = epg_url.Replace("[#EPGID#]", "");

				Console.WriteLine("Downloading EPG from: {0}", epg_url);
				epg_wc.DownloadStringAsync(new Uri(epg_url));
			}
		}

		string GetEPGProviderURL(string id)
		{
			var url = string.Empty;
			if (db.Count("epg_lists", "provider", id) != 0)
			{
				var urls = db.SQLQuery<ulong>(string.Format("SELECT url FROM epg_lists WHERE provider='{0}'", id));
				url = urls[(ulong)new Random().Next(0, urls.Count - 1)]["url"];
			}

			return url;
		}

		string GetEPGSource(string provider)
		{
			return db.SQLQuery(string.Format("SELECT url FROM epg_lists WHERE provider='{0}'", provider), "url");
		}

		XmlDocument GenerateEPG()
		{
			var xml = new XmlDocument();
			var root = xml.CreateElement("tv");

			root.SetAttribute("generator-info-name", "Rytec");
			root.SetAttribute("generator-info-url", "http://forums.openpli.org");

			/*
			var channels = db.SQLQuery("SELECT * FROM channels");
			if (channels.Count != 0)
			{
				for (var i = ulong.MinValue; i < (ulong)channels.Count; i++)
				{
					if (channels[i]["epgid"] == "noepg.epg")
						continue;

					var channel = xml.CreateElement("channel");
					channel.SetAttribute("id", channels[i]["epgid"]);

					var dn = xml.CreateElement("display-name");
					dn.SetAttribute("lang", "de");
					dn.InnerText = channels[i]["name"];
					channel.AppendChild(dn);

					root.AppendChild(channel);
				}
			}

			xml.AppendChild(root);
			xml.InsertBefore(xml.CreateXmlDeclaration("1.0", "UTF-8", null), root);
			*/
			return xml;
		}

		public void HeartBeat()
		{
		}
	}
}
