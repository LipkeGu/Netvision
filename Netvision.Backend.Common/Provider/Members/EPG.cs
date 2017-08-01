using System;

namespace Netvision.Backend.Providers
{
    public sealed class epg
    {
        public string sdate_parsed;
        public string edate_parsed;

        public string etime_parsed;
        public string stime_parsed;

        public string title;
        public string duration;
        public string subtitle;

        public string progid;
        public string cid;

        public string text_text;

        public string Start
        {
            get
            {
                return string.Concat(DateTime.Parse(sdate_parsed).ToString("yyyyMMdd"),
                    string.Concat(DateTime.Parse(stime_parsed).ToString("hhmmss")), " +0100");
            }
        }

        public string Stop
        {
            get
            {
                return string.Concat(DateTime.Parse(edate_parsed).ToString("yyyyMMdd"),
                    string.Concat(DateTime.Parse(etime_parsed).ToString("hhmmss")), " +0100");
            }
        }

        public string CID
        {
            get
            {
                return cid;
            }
        }

        public string ProgID
        {
            get
            {
                return progid;
            }
        }

        public string Length
        {
            get
            {
                return duration;
            }
        }

        public string Title
        {
            get
            {
                return title;
            }
        }

        public string SubTitle
        {
            get
            {
                return subtitle;
            }
        }

        public string Description
        {
            get
            {
                return text_text;
            }
        }
    }
}

