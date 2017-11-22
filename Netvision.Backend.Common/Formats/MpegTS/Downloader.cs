using System;
using System.Net;
using System.IO;
using Netvision.Backend.Provider;
using System.Threading.Tasks;

namespace Netvision.Formats
{
	public class TSDownloader : IDisposable
	{
		public TSDownloader()
		{
		}

		public void Request(Uri url, ref HttpListenerContext context)
		{
			var req = WebRequest.CreateHttp(url);
			req.AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate;
			req.AllowAutoRedirect = true;
			req.Timeout = 40000;

			req.UserAgent = context.Request.UserAgent;
			req.Accept = context.Request.Headers["Accept"];

			req.KeepAlive = string.IsNullOrEmpty(context.Request.Headers["Connection"]) ?
				false : context.Request.Headers["Connection"] == "Close" ? false : true;

			if (!string.IsNullOrEmpty(context.Request.Headers["Pragma"]))
				req.Headers.Add("Pragma", context.Request.Headers["Pragma"]);

			if (context.Request.Cookies.Count != 0)
				req.CookieContainer.Add(context.Request.Cookies);

			if (!string.IsNullOrEmpty(context.Request.Headers["Range"]))
			{
				var rangeParts = context.Request.Headers["Range"].Split('=');
				var rangeValue = rangeParts[1].Split('-');

				req.AddRange(rangeParts[0], int.Parse(rangeValue[0]),
					string.IsNullOrEmpty(rangeValue[1]) ? 0 : int.Parse(rangeValue[1]));
			}

			req.Headers.Add("ETag", !string.IsNullOrEmpty(context.Request.Headers["ETag"]) ?
				context.Request.Headers["ETag"] : MD5.GetMD5Hash(url.OriginalString));

			try
			{
				using (var resp = (HttpWebResponse)req.GetResponse())
				{
					context.Response.Headers.Remove("Server");
					context.Response.Headers.Add("Server", "Netvision IPTV Backend");
					context.Response.ContentType = "video/mp2t";
					context.Response.StatusCode = 200;
					context.Response.StatusDescription = "OK";
					context.Response.KeepAlive = resp.Headers["Connection"] == "Close" ? false : true;
					if (resp.Cookies.Count != 0)
						context.Response.Cookies.Add(resp.Cookies);

					using (var clientStream = new BufferedStream(context.Response.OutputStream))
					{
						using (var ServerStream = new BufferedStream(resp.GetResponseStream()))
						{
							if (context.Request.RemoteEndPoint != null)
							{
								try
								{
									ServerStream.CopyTo(clientStream);
									ServerStream.Close();
								}
								catch (Exception ex)
								{
									ServerStream.Close();
								}
							}
						}

						clientStream.Close();
					}

					resp.Close();
				}
			}
			catch (WebException webEx)
			{
				Console.WriteLine("TSProxy Error: {0}", webEx.Message);
			}
		}

		public void Dispose()
		{
		}
	}
}
