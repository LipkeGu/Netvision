using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace Netvision.Backend.Common
{
	public class Filesystem : IDisposable
	{
		string rootDir;
		int fscache;

		public Filesystem(string path, int cache = 4096)
		{
			this.rootDir = path;
			this.fscache = cache;

			Directory.CreateDirectory(rootDir);
		}

		static string ReplaceSlashes(string path, string curSlash, string newSlash)
		{
			while (path.Contains(curSlash))
				path = path.Replace(curSlash, newSlash);

			return path;
		}

		public static void Delete(string path)
		{
			if (File.Exists(path))
				File.Delete(path);
		}

		public string ResolvePath(string path, bool strip = true)
		{
			var p = path.Trim();

			if (p.StartsWith("/") && p.Length > 3 && strip)
				p = p.Remove(0, 1);

			p = ReplaceSlashes(Combine(rootDir, p), "\\", "/");

			return p.Trim();
		}

		public bool Exists(string path)
		{
			return File.Exists(ResolvePath(path));
		}

		public static string Combine(string p1, params string[] paths)
		{
			var path = p1;
			for (var i = 0; i < paths.Length; i++)
				path = ReplaceSlashes(Path.Combine(path, paths[i]), "\\", "/");

			return path;
		}

		public async Task<byte[]> Read(string path, int offset = 0, int count = 0)
		{
			var data = new byte[0];

			using (var fs = new FileStream(ResolvePath(path), FileMode.Open,
				FileAccess.Read, FileShare.Read, this.fscache, true))
			{
				data = new byte[(count == 0 || fs.Length < count) ? fs.Length : count];
				var bytesRead = 0;

				do
				{
					bytesRead = await fs.ReadAsync(data, 0, data.Length).ConfigureAwait(false);
				} while (bytesRead > 0);
			}

			return data;
		}

		public void Write(string path, ref byte[] data, int offset = 0, int count = 0)
		{
			using (var fs = new FileStream(ResolvePath(path), FileMode.OpenOrCreate, FileAccess.Write))
				fs.WriteAsync(data, 0, count).ConfigureAwait(false);
		}

		public void Write(string path, ref string data, int offset = 0, int count = 0)
		{
			var chars = data.ToCharArray();
			var tmp = Encoding.UTF8.GetBytes(chars, 0, chars.Length);

			Write(path, ref tmp, offset, count);
		}

		public void Dispose()
		{
		}

		public string Root
		{
			get
			{
				return rootDir;
			}
		}
	}
}
