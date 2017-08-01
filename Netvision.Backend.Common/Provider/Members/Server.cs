using System;

namespace Netvision.Backend.Providers
{
    public sealed class Server
    {
        string url;
        int type;

        public Server(string url, int type)
        {
            this.url = url;
            this.type = type;
        }

        public string URL
        {
            get { return url; }
        }

        public int Type
        {
            get { return type; }
        }
    }
}

