using System;
using System.Collections.Generic;

namespace Netvision.Backend.Provider
{
	public sealed class Channel
	{
		int db_chanid;
		int id;
		string name;
		string logo;

		List<Server> servers;

		int provider;
		int type;
		int chanNr;

		public Channel()
		{
			servers = new List<Server>();
			servers.Capacity = byte.MaxValue;
		}

		public int ChanDBID
		{
			get
			{
				return db_chanid;
			}

			set
			{
				db_chanid = value;
			}
		}

		public int ChanNo
		{
			get
			{
				return chanNr;
			}

			set
			{
				chanNr = value;
			}
		}

		public int Provider
		{
			get
			{

				return provider;
			}

			set
			{
				provider = value;
			}
		}

		public string Name
		{
			get
			{

				return name;
			}

			set
			{
				name = value;
			}
		}

		public int ID
		{
			get
			{

				return id;
			}

			set
			{
				id = value;
			}
		}

		public List<Server> Servers
		{
			get
			{
				return servers;
			}
		}

		public string Logo
		{
			get
			{
				return logo;
			}

			set
			{
				logo = value;
			}
		}

		public int Type
		{
			get
			{
				return type;
			}

			set
			{
				type = value;
			}
		}
	}
}
