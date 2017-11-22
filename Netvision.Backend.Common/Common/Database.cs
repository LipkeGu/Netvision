using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Data;
using System.Data.SQLite;
using System.Threading.Tasks;

namespace Netvision.Backend
{
	public class SQLDatabase : IDisposable
	{
		SQLiteConnection sqlConn;

		public SQLDatabase(string database)
		{
			var dataBase = Filesystem.Combine(Environment.CurrentDirectory, database);
			sqlConn = new SQLiteConnection(string.Format("Data Source={0};Version=3;", dataBase));

			Open();
		}

		async void Open() => sqlConn.OpenAsync();

		public async Task<int> Count<T>(string table, string condition, T value)
		{
			var x = 0;

			using (var cmd = new SQLiteCommand(
				string.Format("SELECT Count({0}) FROM {1} WHERE {0}=\"{2}\"",
					condition, table, string.Format("{0}", value)), sqlConn))
			{
				cmd.CommandType = CommandType.Text;
				x = Convert.ToInt32(cmd.ExecuteScalarAsync().Result);
			}

			return x;
		}

		public async Task<Dictionary<T, NameValueCollection>> SQLQuery<T>(string sql)
		{
			var x = new Dictionary<T, NameValueCollection>();
			using (var cmd = new SQLiteCommand(sql, sqlConn))
			{
				cmd.CommandType = CommandType.Text;
				var y = cmd.ExecuteNonQueryAsync().Result;

				var reader = cmd.ExecuteReader();
				var i = 0;

				while (reader.Read())
					if (!x.ContainsKey((T)Convert.ChangeType(i, typeof(T))))
					{
						x.Add((T)Convert.ChangeType(i, typeof(T)), reader.GetValues());
						i++;
					}

				reader.Close();
			}

			return x;
		}

		public async Task<bool> SQLInsert(string sql)
		{
			var result = false;
			using (var cmd = new SQLiteCommand(sql, sqlConn))
				result = cmd.ExecuteNonQueryAsync().Result != 0;

			return result;
		}

		public async Task<string> SQLQuery(string sql, string key)
		{
			var result = string.Empty;
			using (var cmd = new SQLiteCommand(sql, sqlConn))
			{
				cmd.CommandType = CommandType.Text;
				var x = cmd.ExecuteNonQueryAsync().Result;

				var reader = cmd.ExecuteReaderAsync().Result;
				while (reader.Read())
					result = string.Format("{0}", reader[key]);

				reader.Close();
			}

			return result;
		}

		public void Close()
		{
			sqlConn.Close();
		}

		public void HeartBeat()
		{
			sqlConn.ReleaseMemory();
		}

		public void Dispose()
		{
			sqlConn.Dispose();
		}
	}
}
