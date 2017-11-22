using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Web;

namespace Netvision.Backend.Network
{
	public class HTTPSocket
	{
		public enum RequestTarget
		{
			PlayList,
			EPG,
			WebSite,
			StreamProxy
		}

		HttpListener listener;

		public delegate void DataReceivedEventHandler(object sender, DataReceivedEventArgs e);
		public event DataReceivedEventHandler DataReceived;
		public class DataReceivedEventArgs : EventArgs
		{
			public HttpListenerContext Context;

			string ua;
			string path;

			Dictionary<string, string> param;
			BackendTarget target;

			public BackendTarget Target
			{
				get
				{
					return target;
				}

				internal set
				{
					target = value;
				}
			}

			public Dictionary<string, string> Parameters
			{
				get
				{
					return param;
				}

				internal set
				{
					param = value;
				}
			}

			public string Path
			{
				get
				{
					return path;
				}

				internal set
				{
					path = value;
				}
			}

			public string UserAgent
			{
				get
				{
					return ua;
				}

				internal set
				{
					ua = value;
				}
			}
		}

		async void Start()
		{
			listener.Start();
			Task.Run(() => HandleClientConnections(listener).ConfigureAwait(false));
		}

		public HTTPSocket(string domain, int port, string path)
		{
			listener = new HttpListener();
			listener.Prefixes.Add(string.Format("http://{0}:{1}/{2}", domain, port, path));

			Task.Run(() => Start());
		}

		async Task HandleClientConnections(HttpListener listener)
		{
			var context = listener.GetContextAsync().Result;
			var path = GetContentType(context.Request.Url.AbsoluteUri, context).Result;

			var evargs = new DataReceivedEventArgs();
			evargs.Context = context;
			evargs.UserAgent = context.Request.UserAgent;
			evargs.Parameters = GetPostData(path, context).Result;

			if (path.EndsWith("/epg/") || path.Contains("/epg") ||
				path.EndsWith("/playlist/") || path.Contains("/playlist") ||
				path.Contains("/channel/") || path.Contains("/channel") ||
					path.Contains("/tsproxy/") || path.Contains("/tsproxy"))
			{
				if (path.EndsWith("/epg/") || path.Contains("/epg"))
				{
					evargs.Target = BackendTarget.Epg;
					evargs.Path = path;
				}
				else
				{
					if (path.EndsWith("/playlist/") || path.Contains("/playlist"))
					{
						evargs.Target = BackendTarget.Playlist;
						evargs.Path = path;
					}
					else
					{
						if (path.EndsWith("/tsproxy/") || path.Contains("/tsproxy"))
						{
							evargs.Target = BackendTarget.TSProxy;
							evargs.Path = path;
						}
						else
						{
							evargs.Target = BackendTarget.Channel;
							evargs.Path = path;
						}
					}
				}
			}

			Task.Run(() => DataReceived?.Invoke(this, evargs));

			Task.Run(() => HandleClientConnections(listener).ConfigureAwait(false));
		}

		public static async Task<Dictionary<string, string>> GetPostData(string path, HttpListenerContext context)
		{
			var formdata = new Dictionary<string, string>();
			var tmp = HttpUtility.UrlDecode(path);

			switch (context.Request.HttpMethod)
			{
				case "POST":
					var encoding = context.Request.ContentEncoding;
					var line = string.Empty;

					using (var reader = new StreamReader(context.Request.InputStream, encoding))
						line = reader.ReadToEndAsync().Result;

					if (string.IsNullOrEmpty(line))
						return formdata;

					if (!string.IsNullOrEmpty(context.Request.ContentType))
					{
						if (context.Request.ContentType.Split(';')[0] != "application/x-www-form-urlencoded")
						{
							var boundary = context.Request.ContentType.Split('=')[1];

							if (string.IsNullOrEmpty(line))
								return null;

							var start = line.IndexOf(boundary) + (boundary.Length + 2);
							var end = line.LastIndexOf(boundary) + boundary.Length;
							line = line.Substring(start, end - start);
							var formparts = new List<string>();

							while (line.Contains(boundary))
								if (line.StartsWith("Content-Disposition:"))
								{
									start = line.IndexOf("Content-Disposition: form-data;") +
									"Content-Disposition: form-data;".Length;

									end = line.IndexOf(boundary);
									formparts.Add(line.Substring(start, end - start).TrimStart());
									line = line.Remove(0, end);
								}

								if (line.StartsWith(boundary))
								{
									if (line.Length > boundary.Length + 2)
										line = line.Remove(0, boundary.Length + 2);
									else
										break;
								}

							if (formparts.Count != 0)
							{
								foreach (var item in formparts)
									if (item.Contains("filename=\""))
									{
										var posttag = item.Substring(0, item.IndexOf(";"));
										var data = item;
										start = data.IndexOf("filename=\"") + "filename=\"".Length;
										data = data.Remove(0, start);
										end = data.IndexOf("\"");

										var filename = data.Substring(0, end);
										if (string.IsNullOrEmpty(filename))
											continue;

										if (filename.Contains("\\") || filename.Contains("/"))
										{
											var parts = filename.Split(filename.Contains("\\") ? '\\' : '/');
											filename = parts[parts.Length - 1];
										}

										start = data.IndexOf("Content-Type: ");
										data = data.Remove(0, start);
										end = data.IndexOf("\r\n");
										data = data.Remove(0, end + 2);

										var filedata = context.Request.ContentEncoding
											.GetBytes(data.Substring(2, data.IndexOf("\r\n--")));

										var uploadpath = Filesystem.Combine(tmp, filename);
										Task.Run(() => File.WriteAllBytes(uploadpath, filedata));

										if (!formdata.ContainsKey(posttag))
											formdata.Add(posttag, uploadpath);
									}
									else
									{
										var x = item.Replace("\r\n--", string.Empty).Replace("name=\"",
											string.Empty).Replace("\"", string.Empty).Replace("\r\n\r\n", "|").Split('|');
										x[0] = x[0].Replace(" file", string.Empty);

										if (!formdata.ContainsKey(x[0]))
											formdata.Add(x[0], x[1]);
									}

								formparts.Clear();
							}

							formparts = null;
						}
						else
						{
							var xtmp = line.Split('&');
							for (var i = 0; i < xtmp.Length; i++)
								if (xtmp[i].Contains("="))
								{
									var p = xtmp[i].Split('=');
									if (!formdata.ContainsKey(p[0]) && p.Length != 0)
										formdata.Add(p[0], HttpUtility.UrlDecode(p[1]).ToString());
								}
						}
					}
					break;
				case "GET":
					if (tmp.Contains("?") && tmp.Contains("="))
					{
						var get_params = tmp.Contains("&") ?
							tmp.Split('?')[1].Split('&') : tmp.Split('?');

						for (var i = 0; i < get_params.Length; i++)
							if (get_params[i].Contains("="))
							{
								var p = get_params[i].Split('=');
								if (!formdata.ContainsKey(p[0]))
									formdata.Add(p[0], p[1]);
							}
					}

					if (tmp.Contains("|"))
					{
						var get_params = tmp.Split('|');

						for (var i = 0; i < get_params.Length; i++)
							if (get_params[i].Contains("="))
							{
								var p = get_params[i].Split('=');
								if (!formdata.ContainsKey(p[0]))
									formdata.Add(p[0], p[1]);
							}
					}
					break;
				default:
					break;
			}

			return formdata;
		}

		public static async Task<string> GetContentType(string path, HttpListenerContext context)
		{
			using (var db = new SQLDatabase("channels.db"))
			{
				var ext = path.Contains(".") ? path.Split('.')[1] : string.Empty;
				var ctype = string.Empty;

				if (ext.Length > 1)
					if (await db.Count("content_types", "ext", ext[1]) != 0)
					{
						ctype = await db.SQLQuery(string.Format("SELECT type FROM content_types WHERE ext='{0}'",
							string.Concat(".", ext[ext.Length - 1])), "type");

						context.Response.ContentType = ctype;
					}
			}

			return path.ToLowerInvariant();
		}

		public async void Send(string data, HttpListenerContext context)
		{
			if (context == null)
				return;

			await Task.Run(() => Send(Encoding.UTF8.GetBytes(data), context));
		}

		public async void Send(byte[] data, HttpListenerContext context)
		{
			if (context == null || context.Response.OutputStream == null)
				return;

			using (var strm = new BufferedStream(context.Response.OutputStream))
			{
				try
				{
					context.Response.ContentLength64 = data.Length;
					strm.WriteAsync(data, 0, data.Length);
				}
				catch (Exception ex)
				{
				}
			}
		}

		public void Close()
		{
			if (listener.IsListening)
				listener.Stop();

			listener.Close();
		}
	}
}
