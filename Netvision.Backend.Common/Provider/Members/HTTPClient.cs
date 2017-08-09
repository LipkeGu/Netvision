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

		string contentType;
		string useragent;
		int statuscode;

		long contentLength;

		public HTTPClient(string url)
		{
			wc = WebRequest.CreateHttp(url);
			wc.AutomaticDecompression = DecompressionMethods.GZip;
		}

		public async Task<byte[]> GetResponse()
		{
			var x = new byte[0];

			try
			{
				wc.UserAgent = useragent;
				wc.KeepAlive = false;

				using (var response = (HttpWebResponse)await wc.GetResponseAsync())
				{
					contentLength = response.ContentLength;
					contentType = response.ContentType.Split(';')[0];
					statuscode = (int)response.StatusCode;

					using (var strm = response.GetResponseStream())
						using (var str = new StreamReader(strm))
						{
							var s = await str.ReadToEndAsync();
							x = Encoding.UTF8.GetBytes(s);
							contentLength = s.Length;
						}
				}
			}
			catch (Exception ex)
			{
				await Task.Run(() => Console.WriteLine("HTTP Error: {0}", ex.Message));
			}

			return x;
		}

		public long ContentLength
		{
			get { return contentLength; }
		}

		public string ContentType
		{
			get { return contentType; }
		}

		public int StatusCode
		{
			get { return statuscode; }
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
