using System;
using System.Text;
using System.Net;

namespace Netvision.Backend
{
    public class TemplateProvider
    {
        Filesystem fs;
        public TemplateProvider()
        {
            fs = new Filesystem("");
        }

        public string ReadTemplate(string name)
        {
            var tpl_path = string.Format("templates/{0}.tpl", name.Replace("-", "_"));
            if (!fs.Exists(tpl_path))
                throw new Exception(string.Format("Template (name: {0}): Not Found!", name));
            else
            {
                var output = fs.Read(tpl_path).Result;

                return Encoding.UTF8.GetString(output, 0, output.Length);
            }
        }
    }
}

