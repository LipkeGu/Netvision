using System;
using System.Collections.Generic;
using System.Net;

namespace Netvision.Backend.Network
{
	public class Backend
	{
		BackendHub backendhub;

		public delegate void BackendResponseEventHandler(object sender, BackendResponseEventArgs e);
		public event BackendResponseEventHandler BackendResponse;
		public class BackendResponseEventArgs : EventArgs
		{
			public BackendAction Action;
			public HttpListenerContext Context;
			public Dictionary<string, string> Parameters;
			public int Provider;
			public string Response;
			public BackendTarget Target;
		}

		public delegate void BackendHubRequestEventHandler(object sender, BackendHubRequestEventArgs e);
		public event BackendHubRequestEventHandler BackendHubRequest;
		public class BackendHubRequestEventArgs : EventArgs
		{
			public HttpListenerContext Context;
			public string Response;

			public BackendAction Action;
			public BackendTarget Target;
			public Dictionary<string, string> Parameters;
		}

		public void HeartBeat()
		{
			backendhub?.HeartBeat();
		}

		public Backend(ref SQLDatabase db, Netvision.Backend.Backend backend)
		{
			backendhub = new BackendHub(ref db, this);

			backend.BackendRequest += (sender, e) =>
			{
				var evArgs = new BackendHubRequestEventArgs();
				evArgs.Context = e.Context;
				evArgs.Action = e.Action;
				evArgs.Target = e.Target;
				evArgs.Parameters = e.Parameters;

				BackendHubRequest?.Invoke(this, evArgs);
			};

			backendhub.BackendHubResponse += (sender, e) =>
			{
				var evArgs = new BackendResponseEventArgs();
				evArgs.Context = e.Context;
				evArgs.Response = e.Response;
				evArgs.Target = e.Target;

				BackendResponse?.Invoke(this, evArgs);
			};
		}
	}
}
