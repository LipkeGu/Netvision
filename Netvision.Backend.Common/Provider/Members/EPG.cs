using System;
using System.Collections.Generic;

namespace Netvision.Backend.Provider
{
	public sealed class epg_entry
	{
		public string sdate_parsed;
		public string edate_parsed;
		public string etime_parsed;
		public string stime_parsed;
		public string production_year;
		public string series_count;
		public string series_number;
		public string relay;
		public string country;
		public string title;
		public string duration;
		public string subtitle;
		public string progid;
		public string cid;
		public string text_text;

		public List<person> person;
		public List<image> allImages;
		public List<category> category_genre;

		public string Series_Episode
		{
			get
			{
				return series_number;
			}
		}

		public string Series_Episodes
		{
			get
			{
				return series_count;
			}
		}

		public string Country
		{
			get
			{
				return country;
			}
		}

		public string Series_Season
		{
			get
			{
				return relay;
			}
		}

		public string Start
		{
			get
			{
				return string.Concat(DateTime.Parse(sdate_parsed).ToString("yyyyMMdd"),
					string.Concat(DateTime.Parse(stime_parsed).ToString("HHmmss")), " +0000");
			}
		}

		public string Stop
		{
			get
			{
				return string.Concat(DateTime.Parse(edate_parsed).ToString("yyyyMMdd"),
					string.Concat(DateTime.Parse(etime_parsed).ToString("HHmmss")), " +0000");
			}
		}

		public string CID
		{
			get
			{
				return cid;
			}
		}

		public string Date
		{
			get
			{
				return production_year;
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

		public List<person> Person
		{
			get
			{
				return person;
			}
		}

		public List<category> Categories
		{
			get
			{
				return category_genre;
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

		public string Images
		{
			get
			{
				return allImages[new Random(allImages.Count).Next(0, allImages.Count - 1)].Url;
			}
		}
	}

	public struct person
	{
		public string name;
		public string kind;
	}

	public struct category
	{
		public string text;
	}

	public struct image
	{
		public string image_name;
		public string image_source_type;

		public string Url
		{
			get { return string.Concat(image_source_type, image_name); }
		}
	}
}
