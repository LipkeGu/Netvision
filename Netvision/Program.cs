using System;

namespace Netvision
{
	class Program
	{
		static void Main(string[] args)
		{
			var backend = new Backend.Backend();
			var input = string.Empty;

			while (input != "!exit")
				input = Console.ReadLine();
		}
	}
}
