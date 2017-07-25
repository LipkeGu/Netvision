using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net;
using System.Threading.Tasks;

namespace Netvision.Backend
{
	public class BackendHub
	{
		public delegate void BackendHubResponseEventHandler(object sender, BackendHubResponseEventArgs e);
		public event BackendHubResponseEventHandler BackendHubResponse;
		public class BackendHubResponseEventArgs : EventArgs
		{
			public string Response;
			public HttpListenerContext Context;
		}

		public delegate void PlayListRequestEventHandler(object sender, PlayListRequestEventArgs e);
		public event PlayListRequestEventHandler PlayListRequest;
		public class PlayListRequestEventArgs : EventArgs
		{
			public HttpListenerContext Context;
		}

		public delegate void EPGRequestEventHandler(object sender, EPGRequestEventArgs e);
		public event EPGRequestEventHandler EPGRequest;
		public class EPGRequestEventArgs : EventArgs
		{
			public HttpListenerContext Context;
		}

		PlayListProvider playlist;
		EPGProvider epg;
		ChannelProvider channel;

		public BackendHub(Network.Backend backend)
		{
			channel = new ChannelProvider(this);
			
			playlist = new PlayListProvider(this);
			playlist.PlayListResponse += (sender, e) => {
				var evArgs = new BackendHubResponseEventArgs();
				evArgs.Response = e.Response;
				evArgs.Context = e.Context;

				BackendHubResponse?.Invoke(this, evArgs);
			};

			epg = new EPGProvider(this);
			epg.EPGResponse += (sender, e) => {
				var evArgs = new BackendHubResponseEventArgs();
				evArgs.Response = e.Response;
				evArgs.Context = e.Context;

				BackendHubResponse?.Invoke(this, evArgs);
			};

			backend.BackendHubRequest += (sender, e) =>
			{
				switch (e.Target)
				{
					case BackendTarget.Playlist:
						var PLevArgs = new PlayListRequestEventArgs();
						PLevArgs.Context = e.Context;

						PlayListRequest?.Invoke(this, PLevArgs);
						break;
					case BackendTarget.Epg:
						var evArgs = new EPGRequestEventArgs();
						evArgs.Context = e.Context;

						EPGRequest?.Invoke(this, evArgs);
						break;
					default:
						break;
				}
			};
		}

		public void HeartBeat()
		{
			playlist.HeartBeat();
			epg.HeartBeat();
		}
	}
}
