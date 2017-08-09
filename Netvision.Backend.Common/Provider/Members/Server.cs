using System;

namespace Netvision.Backend.Provider
{
	public sealed class Server
	{
		string url;
		int type;
		int ua;

		public Server(string url, int type, int ua)
		{
			this.url = url;
			this.type = type;
			this.ua = ua;
		}

		public string URL
		{
			get { return url; }
		}

		public int Type
		{
			get { return type; }
		}

		public int UserAgent
		{
			get { return ua; }
		}
	}
}
