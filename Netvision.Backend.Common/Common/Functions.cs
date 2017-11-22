using System;
using Netvision.Backend;
using System.Threading.Tasks;
using System.Text;

namespace Netvision
{
	public static class Functions
	{
		public static async Task<string> FirstCharUpper(this string input, SQLDatabase db)
		{
			var result = string.Empty;

			input = input.Replace("-", " ");
			var parts = input.Split(' ');
			var i = 0;

			if (parts.Length > 1)
			{
				for (i = 0; i < parts.Length; i++)
					if (parts[i].Length != 0)
						result += FirstCharToUpper(parts[i]) + " ";
			}
			else
				if (parts.Length >= 0)
					result += FirstCharToUpper(parts[0]) + " ";

			return ReplaceAndFixNames(db, result).Result;
		}

		public static string ToBase64(string input)
		{
			return Convert.ToBase64String(Encoding.UTF8.GetBytes(input));
		}

		public static string FromBase64(string input, Encoding encoding)
		{
			return Encoding.UTF8.GetString(Convert.FromBase64String(input));
		}

		static string FirstCharToUpper(string str)
		{
			return str[0].ToString().ToUpper() + str.Substring(1).ToLower();
		}

		public async static Task<string> GetUseragentByID(SQLDatabase db, int id)
		{
			return db.SQLQuery(string.Format("SELECT name FROM user_agents WHERE id='{0}'", id), "name").Result;
		}

		public async static Task<int> GetUseragentIDByName(SQLDatabase db, string name)
		{
			return int.Parse(db.SQLQuery(string.Format("SELECT id FROM user_agents WHERE name='{0}'", name), "id").Result);
		}

		public static async Task<string> ReplaceAndFixNames(SQLDatabase db, string input, bool split = true)
		{
			var output = input;
			if (split)
			{
				if (output.Contains("|"))
					output = output.Split('|')[1].Trim();

				if (output.Contains(":"))
					output = output.Split(':')[1].Trim();
			}

			var replaces = db.SQLQuery<ulong>("SELECT * FROM replace").Result;
			if (replaces.Count != 0)
			{
				for (var i = ulong.MinValue; i < (ulong)replaces.Count; i++)
					if (output.Contains(replaces[i]["from"]))
						output = output.Replace(replaces[i]["from"], replaces[i]["to"]).FirstCharUpper(db).Result;
			}

			if (output.Contains("Hd"))
			{
				if (output.EndsWith(" Hd"))
					output = output.Substring(0, output.Length - 2).Trim();

				output = output.Replace(" Hd", string.Empty).Trim();
			}

			if (output.Contains("De"))
			{
				if (output.EndsWith("De"))
					output = output.Substring(0, output.Length - 2).Trim();

				if (output.StartsWith("De "))
					output = output.Substring(3, output.Length - 3).Trim();
			}

			output = output.Replace("--empty--", string.Empty);

			output = output.Replace("&nbsp;", string.Empty);
			output = output.Replace("&#135;", string.Empty);

			output = output.Replace("[#YDAY#]", formatNumberValue(DateTime.Today.AddDays(-1).Day));
			output = output.Replace("[#DAY#]", formatNumberValue(DateTime.Today.Day));
			output = output.Replace("[#MONTH#]", formatNumberValue(DateTime.Today.Month));
			output = output.Replace("[#YEAR#]", formatNumberValue(DateTime.Today.Year));

			return output.Trim();
		}

		public static string formatNumberValue(this int number)
		{
			return (number < 10) ? string.Format("0{0}", number) : string.Format("{0}", number);
		}

		public static string AsString(this int i) => string.Format("{0}", i);
	}
}
