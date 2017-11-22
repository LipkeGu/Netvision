using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net;
using System.Threading.Tasks;
using Netvision.Backend;

namespace Netvision.Backend.Network
{

	public class WebSite
	{
		WebSiteHub websitehub;

		public delegate void WebSiteResponseEventHandler(object sender, WebSiteResponseEventArgs e);
		public event WebSiteResponseEventHandler WebSiteResponse;
		public class WebSiteResponseEventArgs : EventArgs
		{
			public HttpListenerContext Context;
			public string Response;
		}

		public delegate void WebSiteHubRequestEventHandler(object sender, WebSiteHubRequestEventArgs e);
		public event WebSiteHubRequestEventHandler WebSiteHubRequest;
		public class WebSiteHubRequestEventArgs : EventArgs
		{
			public HttpListenerContext Context;
			public string Response;

			public BackendAction Action;
			public BackendTarget Target;
			public Dictionary<string, string> Parameters;
		}

		public WebSite(Netvision.Backend.Backend backend)
		{
			websitehub = new WebSiteHub(this);
			websitehub.WebSiteHubResponse += (sender, e) =>
			{
				var evArgs = new WebSiteResponseEventArgs();
				evArgs.Context = e.Context;
				evArgs.Response = e.Response;

				WebSiteResponse?.Invoke(this, evArgs);
			};

			backend.WebSiteRequest += (sender, e) =>
			{
				var evArgs = new WebSiteHubRequestEventArgs();
				evArgs.Context = e.Context;
				evArgs.Parameters = e.Parameters;

				WebSiteHubRequest?.Invoke(this, evArgs);
			};
		}
	}
}
