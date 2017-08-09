using System;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json;

namespace Netvision.Backend.Provider
{
	public sealed class epg_entry
	{
		public string sdate_parsed;
		public string edate_parsed;

		public string etime_parsed;
		public string stime_parsed;

		public string title;
		public string duration;
		public string subtitle;
		public List<string> person_names;

		public string progid;
		public string cid;

		public string text_text;

		public string Start
		{
			get
			{
				return string.Concat(DateTime.Parse(sdate_parsed).ToString("yyyyMMdd"),
					string.Concat(DateTime.Parse(stime_parsed).ToString("HHmmss")), " +0100");
			}
		}

		public string Stop
		{
			get
			{
				return string.Concat(DateTime.Parse(edate_parsed).ToString("yyyyMMdd"),
					string.Concat(DateTime.Parse(etime_parsed).ToString("HHmmss")), " +0100");
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

		public List<string> Actors
		{
			get
			{
				return person_names;
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

	public struct epg_metadata
	{
		public string progid;
		public string title;
	}
}
