using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;

namespace Netvision.Backend
{
	struct Vodafone_epg
	{
		/*
		 * 
		
			
			
			/tv-manager/backend/auth-service/proxy/epg-data-service/epg/tv/data/item/1009017106 
		*/

		public string end_date_ger;
		public ulong update_timestamp;
		public string start_date_time;
		public string image_src_height;
		public string title;
		public string progid;
		public string format_cat;
		public string image_name;
		public string groups;
		public string category_format_id;
		public string g;
		public string channelName;
		public string c;
		public string channelid;
		public string categoryIdsString;
		public string start_date_ger;
		public string series_id;
		public List<string> category_ids;
		public string end_date_time;
		public string duration;
		public string startdate;
		public string end_time;
		public string image_src_width;
		public string p;
		public string start_time;
		public string enddate;
		public string start_date;
		public string id;

		public string Start
		{
			get
			{
				return start_date_time.Replace(":", string.Empty).Replace("-", string.Empty);
			}
		}

		public string Stop
		{
			get
			{
				return end_date_time.Replace(":", string.Empty).Replace("-", string.Empty);
			}
		}

		public string Channel
		{
			get
			{
				return channelName;
			}
		}
	}
}
