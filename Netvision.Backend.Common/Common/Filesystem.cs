using System;
using System.IO;
using System.IO.Compression;
using System.Linq;

using System.Text;
using System.Threading.Tasks;

namespace Netvision.Backend
{
	public class Filesystem : IDisposable
	{
		string rootDir;
		int fscache;

		public Filesystem(string path = "", int cache = 4096)
		{
			rootDir = !string.IsNullOrEmpty(path) ? path : Directory.GetCurrentDirectory();
			fscache = cache;

			Directory.CreateDirectory(rootDir);
		}

		/// <summary>
		/// Replaces the path slashes.
		/// </summary>
		/// <returns>The slashes.</returns>
		/// <param name="path">Path.</param>
		/// <param name="curSlash">Current slash.</param>
		/// <param name="newSlash">New slash.</param>
		static string ReplaceSlashes(string path, string curSlash, string newSlash)
		{
			while (path.Contains(curSlash))
				path = path.Replace(curSlash, newSlash);

			return path;
		}

		/// <summary>
		/// Delete the specified path.
		/// </summary>
		/// <param name="path">Path.</param>
		public void Delete(string path)
		{
			if (File.Exists(path))
				File.Delete(path);
		}

		/// <summary>
		/// Resolves the path.
		/// </summary>
		/// <returns>The path.</returns>
		/// <param name="path">Path.</param>
		/// <param name="strip">If set to <c>true</c> strip.</param>
		public string ResolvePath(string path, bool strip = true)
		{
			var p = path.Trim();

			if (p.StartsWith("/") && p.Length > 3 && strip)
				p = p.Remove(0, 1);

			p = ReplaceSlashes(Combine(rootDir, p), "\\", "/").Trim();

			return p;
		}

		/// <summary>
		/// Does the Path exists?
		/// </summary>
		/// <param name="path">Path.</param>
		public bool Exists(string path) => File.Exists(ResolvePath(path));

		/// <summary>
		/// Combines the specified paths.
		/// </summary>
		/// <param name="p1">Base path</param>
		/// <param name="paths">Paths to combine</param>
		public static string Combine(string p1, params string[] paths)
		{
			var path = p1;
			for (var i = 0; i < paths.Length; i++)
				path = ReplaceSlashes(Path.Combine(path, paths[i]), "\\", "/");

			return path;
		}

		/// <summary>
		/// Reads a file in text mode..
		/// </summary>
		/// <returns>The text.</returns>
		/// <param name="path">Path.</param>
		/// <param name="encoding">Encoding.</param>
		public async Task<string> ReadText(string path, Encoding encoding) => encoding.GetString(await Read(path));

		/// <summary>
		/// Read the specified file.
		/// </summary>
		/// <param name="file">File.</param>
		/// <param name="offset">Offset.</param>
		/// <param name="count">Count.</param>
		public async Task<byte[]> Read(string file, int offset = 0, int count = 0)
		{
			var data = new byte[0];

			using (var fs = new FileStream(ResolvePath(file), FileMode.Open,
				FileAccess.Read, FileShare.Read, fscache, true))
			{
				data = new byte[(count == 0 || fs.Length < count) ? fs.Length : count];
				var bytesRead = 0;

				do
				{
					bytesRead = await fs.ReadAsync(data, 0, data.Length);
				} while (bytesRead > 0);
			}

			return data;
		}

		/// <summary>
		/// Write the specified path, data, offset and count.
		/// </summary>
		/// <param name="path">Path.</param>
		/// <param name="data">Data.</param>
		/// <param name="offset">Offset.</param>
		/// <param name="count">Count.</param>
		public async void Write(string path, byte[] data, int offset = 0, int count = 0)
		{
			using (var fs = new FileStream(ResolvePath(path),
				FileMode.OpenOrCreate, FileAccess.Write))
				await fs.WriteAsync(data, 0, count);
		}

		/// <summary>
		/// Write the specified path, data, offset and count.
		/// </summary>
		/// <param name="path">Path.</param>
		/// <param name="data">Data.</param>
		/// <param name="offset">Offset.</param>
		/// <param name="count">Count.</param>
		public void Write(string path, ref string data, int offset = 0, int count = 0)
		{
			var chars = data.ToCharArray();
			var tmp = Encoding.UTF8.GetBytes(chars, 0, chars.Length);

			Write(path, tmp, offset, count);
		}

		/// <summary>
		/// compress the file using GZip.
		/// </summary>
		/// <param name="filename">File to compress.</param>
		public async void GZipCompress(string filename)
		{
			using (var fstream = File.OpenRead(ResolvePath(filename)))
			{
				var f = string.Concat(ResolvePath(filename), ".gz");
				Delete(f);

				using (var gzfile = File.Create(ResolvePath(f)))
				{
					using (var zipstream = new GZipStream(gzfile, CompressionMode.Compress))
					{
						await fstream.CopyToAsync(zipstream);
						zipstream.Close();
					}

					gzfile.Close();
				}

				fstream.Close();
			}
		}

		public void Dispose()
		{
		}

		/// <summary>
		/// Gets the root path.
		/// </summary>
		/// <value>The root.</value>
		public string Root
		{
			get
			{
				return rootDir;
			}
		}
	}
}
