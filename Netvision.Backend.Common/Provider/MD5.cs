using System;
using System.Security.Cryptography;
using System.Text;

namespace Netvision.Backend.Provider
{
	public static class MD5
	{
		public static string GetMD5Hash(string text)
		{
			if (string.IsNullOrEmpty(text))
				return string.Empty;

			var result = (byte[])null;
			using (var md5 = new MD5CryptoServiceProvider())
				result = md5.ComputeHash(Encoding.ASCII.GetBytes(text));

			return BitConverter.ToString(result).Replace("-", string.Empty).ToLower();
		}
	}
}
