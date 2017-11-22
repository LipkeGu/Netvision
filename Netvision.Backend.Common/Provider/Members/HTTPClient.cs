using System;
using System.IO;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace Netvision.Backend.Provider
{
	public sealed class HTTPClient
	{
		HttpWebRequest wc;
		public delegate void HTTPClientErrorEventHandler(object sender, HTTPClientErrorEventArgs e);
		public event HTTPClientErrorEventHandler HTTPClientError;
		public class HTTPClientErrorEventArgs : EventArgs
		{
			public string Message;
			public HTTPClientErrorEventArgs(string message)
			{
				this.Message = message;
			}
		}

		public delegate void HTTPClientResponseEventHandler(object sender, HTTPClientResponseEventArgs e);
		public event HTTPClientResponseEventHandler HTTPClientResponse;
		public class HTTPClientResponseEventArgs : EventArgs
		{
			public string Data;
			public HTTPClientResponseEventArgs(string data)
			{
				this.Data = data;
			}
		}

		string contentType;
		string useragent;
		string method;
		string url;

		HttpStatusCode statuscode;

		long contentLength;

		public HTTPClient(string url, string ua = "", string method = "GET")
		{
			this.method = method;
			if (!string.IsNullOrEmpty(ua))
				this.useragent = ua;

			this.url = url;

			contentType = string.Empty;
			useragent = string.Empty;
			statuscode = HttpStatusCode.NotFound;

			wc = WebRequest.CreateHttp(url);
			wc.AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate;
		}

		public void GetResponse(string name, bool etag = false, bool icyClient = false)
		{
			try
			{
				wc.UserAgent = useragent;
				wc.Method = method;
				if (etag)
				{
					wc.Headers.Add("ETag", MD5.GetMD5Hash(this.url));
					wc.Headers.Add("Icy-Metadata", "1");
				}

				using (var response = (HttpWebResponse)wc.GetResponse())
				{
					contentLength = response.ContentLength;
					contentType = response.ContentType.Split(';')[0];
					statuscode = response.StatusCode;

					using (var str = new StreamReader(new BufferedStream(response.GetResponseStream()), true))
					{
						var result = str.ReadToEnd();
						Task.Run(() => HTTPClientResponse?.Invoke(this, new HTTPClientResponseEventArgs(result)));

						str.BaseStream.Close();
						str.Close();
					}
				}

				
			}
			catch (Exception ex)
			{
				Task.Run(() =>
				{
					HTTPClientError?.Invoke(this, new HTTPClientErrorEventArgs(
						string.Format("HTTPClient Error: ({0}) {1}", name, ex.Message)));
				});
			}
		}

		public long ContentLength
		{
			get { return contentLength; }
		}

		public string ContentType
		{
			get { return contentType.ToLowerInvariant(); }
		}

		public HttpStatusCode StatusCode
		{
			get { return statuscode; }
		}

		public string Method
		{
			get { return method; }
		}

		public string UserAgent
		{
			get
			{
				return useragent;
			}

			set
			{
				useragent = value;
			}
		}
	}
}
