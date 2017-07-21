using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net;

using System.Threading.Tasks;

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

		public EPGProvider(BackendHub backend)
		{
			backend.EPGRequest += (sender, e) => {

			};
		}
	}
}
