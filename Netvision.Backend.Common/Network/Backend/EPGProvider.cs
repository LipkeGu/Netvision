using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Xml;
using Newtonsoft.Json;
using System.IO;
using System.Threading.Tasks;
using System.Text;
using Netvision.Backend.Provider;

namespace Netvision.Backend
{
	public class EPGProvider : IProvider
	{
		public delegate void EPGProviderResponseEventHandler(object sender, EPGProviderResponseEventArgs e);
		public event EPGProviderResponseEventHandler EPGProviderResponse;
		public class EPGProviderResponseEventArgs : EventArgs
		{
			public BackendAction Action;
			public string Response;
			public int Provider;
			public HttpListenerContext Context;
			public Dictionary<string, string> Parameters;
		}

		Dictionary<string, List<epg_entry>> EPGData;

		SQLDatabase db;
		Filesystem fs;
		bool epg_init = false;

		public EPGProvider(ref SQLDatabase db, BackendHub backend)
		{
			fs = new Filesystem(Directory.GetCurrentDirectory());
			this.db = db;

			backend.EPGRequest += (sender, e) =>
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

		async Task<string> GetEPGProviderURL(int provider)
		{
			var url = string.Empty;
			if (await db.Count("epg_lists", "provider", provider.AsString()) != 0)
			{
				var urls = await db.SQLQuery<ulong>(string.Format("SELECT url FROM epg_lists WHERE provider='{0}'", provider.AsString()));
				var days = await db.SQLQuery(string.Format("SELECT days FROM epg_lists WHERE provider='{0}'", provider.AsString()), "days");

				var epg_timespan = DateTime.Today.AddDays(-1);
				url = urls[(ulong)new Random().Next(0, urls.Count - 1)]["url"];

				var IDs = ChannelProvider.GetChannelIDs();

				url = url.Replace("[#EPGID#]", string.Join(",", IDs));
				url = url.Replace("[#SDAY#]", Functions.formatNumberValue(epg_timespan.Day));
				url = url.Replace("[#SMONTH#]", Functions.formatNumberValue(epg_timespan.Month));
				url = url.Replace("[#YEAR#]", Functions.formatNumberValue(epg_timespan.Year));
				url = url.Replace("[#EDAY#]", Functions.formatNumberValue(epg_timespan.AddDays(double.Parse(days)).Day));
				url = url.Replace("[#EMONTH#]", Functions.formatNumberValue(epg_timespan.AddDays(double.Parse(days)).Month));
			}

			return url;
		}

		public async void Create(int provider, string response,
			Dictionary<string, string> parameters, HttpListenerContext context)
		{
			if (EPGData != null)
			{
				var xml = new XmlDocument();
				var root = xml.CreateElement("tv");

				root.SetAttribute("generator-info-name", string.Format("Generated from {0} EPG data by Netvision IPTV Backend",
					await ChannelProvider.GetProviderNameByID(db, provider)));

				root.SetAttribute("generator-info-url", "https://github.com/LipkeGu/Netvision/");

				var channels = ChannelProvider.GetChannels();

				foreach (var channel in channels)
				{
					if (channel.ID == 0)
						continue;

					var cnode = xml.CreateElement("channel");
					cnode.SetAttribute("id", channel.ID.AsString());

					var dn = xml.CreateElement("display-name");
					dn.SetAttribute("lang", "de");
					dn.InnerText = channel.Name;
					cnode.AppendChild(dn);

					root.AppendChild(cnode);
				}

				await Task.Run(() => Console.WriteLine("Generating epgdata.xml.gz..."));
				var epgchannels = (from c in EPGData where c.Value.Count() != 0 select c).OrderBy(c => c.Key);

				foreach (var item in epgchannels)
					for (var i = 0; i < item.Value.Count(); i++)
						lock (item.Value[i])
						{
							var programm = xml.CreateElement("programme");
							programm.SetAttribute("start", item.Value[i].Start);
							programm.SetAttribute("stop", item.Value[i].Stop);
							programm.SetAttribute("channel", item.Key);

							var title = xml.CreateElement("title");
							title.SetAttribute("lang", "de");
							title.InnerText = item.Value[i].Title;
							programm.AppendChild(title);

							if (!string.IsNullOrEmpty(item.Value[i].Description))
							{
								var desc = xml.CreateElement("desc");
								desc.SetAttribute("lang", "de");
								desc.InnerText = item.Value[i].Description;
								programm.AppendChild(desc);
							}

							if (item.Value[i].Person != null)
								if (item.Value[i].Person.Count != 0)
								{
									var credits = xml.CreateElement("credits");

									var actors = (from a in item.Value[i].Person
												  where a.kind == "actor"
												  select a.name);

									foreach (var a in actors)
									{
										var actor = xml.CreateElement("actor");
										actor.InnerText = a;
										credits.AppendChild(actor);
									}

									var directors = (from a in item.Value[i].Person
													 where a.kind == "director"
													 select a.name);

									foreach (var d in directors)
									{
										var director = xml.CreateElement("director");
										director.InnerText = d;

										credits.AppendChild(director);
									}

									programm.AppendChild(credits);
								}

							var icon = item.Value[i].Images;
							if (!string.IsNullOrEmpty(icon))
							{
								var ico = xml.CreateElement("icon");
								ico.SetAttribute("src", icon);

								programm.AppendChild(ico);
							}


							if (!string.IsNullOrEmpty(item.Value[i].Series_Episodes))
							{
								var episode = xml.CreateElement("episode-num");
								episode.SetAttribute("system", "onscreen");
								episode.InnerText = string.Format("S{0} E{1}/{2}",
									item.Value[i].Series_Season,
									item.Value[i].Series_Episode,
									item.Value[i].Series_Episodes);

								programm.AppendChild(episode);
							}

							if (!string.IsNullOrEmpty(item.Value[i].Date))
							{
								var year = xml.CreateElement("date");
								year.InnerText = item.Value[i].Date;

								programm.AppendChild(year);
							}

							if (!string.IsNullOrEmpty(item.Value[i].Country))
							{
								var country = xml.CreateElement("country");
								country.InnerText = item.Value[i].Country;

								programm.AppendChild(country);
							}

							foreach (var cat in item.Value[i].Categories)
							{
								var c = xml.CreateElement("category");
								c.SetAttribute("lang", "de");
								c.InnerText = cat.text;

								programm.AppendChild(c);
							}

							if (!string.IsNullOrEmpty(item.Value[i].SubTitle))
							{
								var ss = xml.CreateElement("sub-title");
								ss.SetAttribute("lang", "de");
								ss.InnerText = item.Value[i].SubTitle;

								programm.AppendChild(ss);
							}

							root.AppendChild(programm);
						}

				xml.AppendChild(root);
				xml.InsertBefore(xml.CreateXmlDeclaration("1.0", "UTF-8", null), root);

				fs.Delete("epgdata.xml");
				xml.Save("epgdata.xml");

				await Task.Run(() =>
				{
					fs.GZipCompress("epgdata.xml");
					Console.WriteLine("EPG informations saved to: epgdata.xml.gz...");
				});
			}

			MakeResponse(provider, string.Empty, BackendAction.Create, parameters, context);
		}

		string fix_json_input(string input)
		{
			var json_input = input;

			json_input = json_input.Replace("\"start_date_ger\"", "\"sdate_parsed\"");
			json_input = json_input.Replace("\"end_date_ger\"", "\"eDate_parsed\"");
			json_input = json_input.Replace("\"startDate\"", "\"sdate_parsed\"");
			json_input = json_input.Replace("\"endDate\"", "\"eDate_parsed\"");
			json_input = json_input.Replace("\"length\"", "\"duration\"");
			json_input = json_input.Replace("\"end_time\"", "\"eTime_parsed\"");
			json_input = json_input.Replace("\"endTime\"", "\"eTime_parsed\"");
			json_input = json_input.Replace("\"start_time\"", "\"sTime_parsed\"");
			json_input = json_input.Replace("\"startTime\"", "\"sTime_parsed\"");
			json_input = json_input.Replace("\"person_us_names\"", "\"person_names\"");

			return json_input;
		}

		async Task<Dictionary<string, List<epg_entry>>> ImportFormJson(string input, int provider)
		{
			var epginfo = new Dictionary<string, List<epg_entry>>();
			using (var db = new SQLDatabase("channels.db"))
			{
				var url = await db.SQLQuery(string.Format("SELECT infourl FROM epg_lists WHERE provider='{0}'", provider), "infourl");
				var json_input = fix_json_input(input);
				var metadata = JsonConvert.DeserializeObject<Dictionary<string, List<epg_entry>>>(json_input);
				foreach (var item in metadata.OrderBy(c => c.Key))
				{
					var dir = string.Format("Download/epg/proginfo/{0}/{1}", provider, item.Key);
					var infolist = new List<epg_entry>();
					infolist.Capacity = ushort.MaxValue;

					Directory.CreateDirectory(dir);

					for (var i = 0; i < item.Value.Count; i++)
					{
						using (var fs = new Filesystem())
						{
							var res = string.Empty;

							var f = fs.ResolvePath(Filesystem.Combine(dir, string.Format("{0}.json", item.Value[i].progid)));

							if (!fs.Exists(f) || fs.Length(f) == 0)
							{
								var hc = new HTTPClient(url.Replace("[#PROGID#]", item.Value[i].ProgID));
								res = fix_json_input(Encoding.UTF8.GetString(await hc.GetResponse()));

								await Task.Run(() => fs.Write(f, res));
							}
							else
								res = await fs.ReadText(f, Encoding.UTF8);

							infolist.Add(JsonConvert.DeserializeObject<epg_entry>(res));
						}
					}

					epginfo.Add(item.Key, infolist);
				}
			}

			return epginfo;
		}

		public async void Download(int provider, string response, Dictionary<string, string> parameters, HttpListenerContext context)
		{
			var epg_url = await GetEPGProviderURL(provider);

			var wc = new HTTPClient(epg_url);
			var res = Encoding.UTF8.GetString(await wc.GetResponse());

			if (!string.IsNullOrEmpty(res))
				EPGData = await ImportFormJson(res, provider);

			MakeResponse(provider, response, BackendAction.Download, parameters, context);
		}

		public void Import(int provider, string response,
			Dictionary<string, string> parameters, HttpListenerContext context)
		{
		}

		public void Update(int provider, string response,
			Dictionary<string, string> parameters, HttpListenerContext context) =>
			Download(provider, response, parameters, context);

		public void MakeResponse(int provider, string response, BackendAction action,
			Dictionary<string, string> parameters, HttpListenerContext context)
		{
			var evArgs = new EPGProviderResponseEventArgs();
			evArgs.Provider = provider;
			evArgs.Action = action;
			evArgs.Response = response;
			evArgs.Context = context;
			evArgs.Parameters = parameters;

			EPGProviderResponse?.Invoke(this, evArgs);
		}

		public void Remove(int provider, string response, Dictionary<string, string> parameters, HttpListenerContext context)
		{
		}

		public void Add(int provider, string response, Dictionary<string, string> parameters, HttpListenerContext context)
		{
		}
	}
}
