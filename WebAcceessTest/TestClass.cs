using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;

namespace WebAcceessTest
{
	[TestFixture]
	public class TestClass
	{
		static void Main(string[] args)
		{
			Console.WriteLine("Starting Test...");
			for (var i = 0; i <  100000; i++)
			{
				var c = new HTTPClient("http://192.168.1.131:81/playlist/");
				c.GetResponse();
			}

			Console.WriteLine("Test finished...");
		}
	}
}
