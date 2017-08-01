using System;
using Netvision.Backend;

namespace Netvision
{
    public static class Functions
    {
        public static string FirstCharUpper(this string input)
        {
            var result = string.Empty;
            var parts = input.Split(' ');
            var i = 0;

            if (parts.Length > 1)
            {
                for (i = 0; i < parts.Length; i++)
                    if (parts[i].Length != 0)
                        result += FirstCharToUpper(parts[i]) + " ";
            }
            else
                result += FirstCharToUpper(parts[i]) + " ";

            return ReplaceAndFixNames(result).Trim();
        }

        static string FirstCharToUpper(string str)
        {
            return str[0].ToString().ToUpper() + str.Substring(1).ToLower();
        }

        static string ReplaceAndFixNames(string input)
        {
            var output = input;

            if (output.Contains("|"))
                output = output.Split('|')[1].Trim();

            if (output.Contains(":"))
                output = output.Split(':')[1].Trim();

            if (output.EndsWith("Hd"))
                output = output.Substring(0, output.Length - 2).Trim();

            output = output.Replace(" Hd ", string.Empty);

            if (output.EndsWith("De"))
                output = output.Substring(0, output.Length - 2).Trim();

            using (var db = new SQLDatabase("channels.db"))
            {
                var replaces = db.SQLQuery<ulong>("SELECT * FROM replace");

                for (var i = ulong.MinValue; i < (ulong)replaces.Count; i++)
                    output = output.Replace(replaces[i]["from"], replaces[i]["to"]);
            }

            output = output.Replace("--empty--", string.Empty);

            output = output.Replace("&nbsp;", string.Empty);
            output = output.Replace("&#135;", string.Empty);

            output = output.Replace("[#YDAY#]", formatNumberValue(DateTime.Today.AddDays(-1).Day));
            output = output.Replace("[#DAY#]", formatNumberValue(DateTime.Today.Day));
            output = output.Replace("[#MONTH#]", formatNumberValue(DateTime.Today.Month));
            output = output.Replace("[#YEAR#]", formatNumberValue(DateTime.Today.Year));

            return output;
        }

        public static string formatNumberValue(int number)
        {
            return (number < 10) ? string.Format("0{0}", number) : string.Format("{0}", number);
        }

        public static string AsString(this int i) => string.Format("{0}", i);

    }
}

