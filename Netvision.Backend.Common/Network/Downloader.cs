using System;
using System.Net;

namespace Netvision.Backend
{
    public class Downloader
    {
        string filename;
        string url;

        public Downloader(string url, string filename)
        {
            this.url = url;
            this.filename = filename;
        }

        public void Start()
        {
            using (var wc = new WebClient())
            {
                wc.DownloadStringCompleted += (sender, e) =>
                {
                        Console.WriteLine("Download completed! ({0})", e.Result);
                };

                wc.DownloadString(new Uri(url.StartsWith("http") ? url : string.Concat("http://", url)));
            }
        }
    }
}

