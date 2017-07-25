using Netvision.Backend;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Data;
using System.Data.SQLite;

namespace Netvision.Backend
{
	public class SQLDatabase : IDisposable
	{
		SQLiteConnection sqlConn;

		public SQLDatabase(string database)
		{
			var dataBase = Filesystem.Combine(Environment.CurrentDirectory, database);
			sqlConn = new SQLiteConnection(string.Format("Data Source={0};Version=3;", dataBase));
			sqlConn.OpenAsync().ConfigureAwait(false);
		}

		public int Count(string table, string condition, string value)
		{
			var x = 0;

			using (var cmd = new SQLiteCommand(string.Format("SELECT Count({0}) FROM {1} WHERE {0}='{2}'",
				condition, table, value), sqlConn))
			{
				cmd.CommandType = CommandType.Text;
				x = Convert.ToInt32(cmd.ExecuteScalar());
			}

			return x;
		}

		public Dictionary<T, NameValueCollection> SQLQuery<T>(string sql)
		{
			Dictionary<T, NameValueCollection> x;

			using (var cmd = new SQLiteCommand(sql, sqlConn))
			{
				cmd.CommandType = CommandType.Text;
				cmd.ExecuteNonQuery();

				x = new Dictionary<T, NameValueCollection>();
				var reader = cmd.ExecuteReader();
				var i = uint.MinValue;

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

		public void SQLInsert(string sql)
		{
			using (var cmd = new SQLiteCommand(sql, sqlConn))
			{
				cmd.ExecuteNonQuery();
			}
		}

		public string SQLQuery(string sql, string key)
		{
			var result = string.Empty;
			using (var cmd = new SQLiteCommand(sql, sqlConn))
			{
				cmd.CommandType = CommandType.Text;
				cmd.ExecuteNonQuery();

				var reader = cmd.ExecuteReader();

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
