using System.Collections.Generic;
using System.Net;

namespace Netvision.Backend.Provider
{
	public interface IProvider
	{
		void Create(int provider, string response, Dictionary<string, string> parameters, HttpListenerContext context);
		void Download(int provider, string response, Dictionary<string, string> parameters,  HttpListenerContext context);
		void Import(int provider, string response, Dictionary<string, string> parameters, HttpListenerContext context);
		void Remove(int provider, string response, Dictionary<string, string> parameters, HttpListenerContext context);
		void Update(int provider, string response, Dictionary<string, string> parameters, HttpListenerContext context);
		void Add(int provider, string response, Dictionary<string, string> parameters, HttpListenerContext context);

		void MakeResponse(int provider, string response, BackendAction action,
			Dictionary<string, string> parameters, HttpListenerContext context);
	}
}
