using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net;

using System.Threading.Tasks;

namespace Netvision.Backend
{
    public class WebSiteHub
    {
        public delegate void WebSiteHubResponseEventHandler(object sender,WebSiteHubResponseEventArgs e);
        public event WebSiteHubResponseEventHandler WebSiteHubResponse;
        public class WebSiteHubResponseEventArgs : EventArgs
        {
            public string Response;
            public HttpListenerContext Context;
        }

        TemplateProvider template;

        public WebSiteHub(Network.WebSite website)
        {
            template = new TemplateProvider();

            website.WebSiteHubRequest += (sender, e) =>
            {
                var evArgs = new WebSiteHubResponseEventArgs();
                    evArgs.Response = template.ReadTemplate("site_content");
                    evArgs.Context = e.Context;

                WebSiteHubResponse?.Invoke(this, evArgs);
            };
        }
    }
}
