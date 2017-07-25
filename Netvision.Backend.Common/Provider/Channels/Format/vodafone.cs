using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Netvision.Backend
{
	/*
	 *	https://tv-manager.vodafone.de/tv-manager/backend/auth-service/proxy/epg-data-service/epg/tv/channels
	 */
	/*
	*	https://tv-manager.vodafone.de/tv-manager/backend/epg_images_channels/ 
	*/
	public struct vodafone_channel
	{
		public string lname;
		public string logo;
		public string ishd;
		public string tvtvid;
	}
}
