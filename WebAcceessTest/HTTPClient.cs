using System;
using System.Net;
using System.Threading.Tasks;

namespace WebAcceessTest
{
	public sealed class HTTPClient
	{
		HttpWebRequest wc;

		public HTTPClient(string url)
		{
			wc = WebRequest.CreateHttp(new Uri(url));
		}

		public async void GetResponse()
		{
			using (var response = await  wc.GetResponseAsync())
			{
				await Task.Run(() => Console.WriteLine("Status: {0}", response.ContentType));
			}
		}
	}
}
