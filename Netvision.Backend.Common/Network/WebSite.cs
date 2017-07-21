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
		public delegate void WebSiteResponseEventHandler(object sender, WebSiteResponseEventArgs e);
		public event WebSiteResponseEventHandler WebSiteResponse;
		public class WebSiteResponseEventArgs : EventArgs
		{
			public HttpListenerContext Context;
			public string Response;
		}

		public WebSite(Netvision.Backend.Backend backend)
		{
			backend.WebSiteRequest += (sender, e) =>
			{
				var evArgs = new WebSiteResponseEventArgs();
				evArgs.Context = e.Context;
				evArgs.Response = string.Empty;

				WebSiteResponse?.Invoke(this, evArgs);
			};
		}
	}
}
