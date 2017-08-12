using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;

namespace Netvision.Backend.Provider
{
	public enum EntryType
	{
		Channel,
		Server,
		EPG,
		Provider
	}

	public class Provider<T>
	{
		Dictionary<string, T> members;
		SQLDatabase db;

		public Provider(ref SQLDatabase db, EntryType type)
		{
			this.db = db;
			members = new Dictionary<string, T>();

			ReadMembersFormDataBase(type);
		}

		async void ReadMembersFormDataBase(EntryType type)
		{
			var table = string.Empty;
			switch (type)
			{
				case EntryType.Channel:
					table = "channels";
					break;
				case EntryType.EPG:
					break;
				case EntryType.Server:
					table = "servers";
					break;
				case EntryType.Provider:
					table = "providers";
					break;
				default:
					break;
			}

			var items = await db.SQLQuery<ulong>(string.Format("SELECT * FROM {0}", table));
			for (var i = ulong.MinValue; i < (ulong)items.Count; i++)
			{
				var item = Activator.CreateInstance(typeof(T));

				if (HasProperty(item.GetType(), "Name"))
				{
					var name_prop = item.GetType().GetProperty("Name");
					name_prop.SetValue(item, items[i]["name"]);

					Add(string.Format("{0}", name_prop.GetValue(item)),
						(T)Convert.ChangeType(item, typeof(T)));
				}
			}
		}

		/// <summary>
		/// Gets the entries.
		/// </summary>
		/// <value>The entries.</value>
		public Dictionary<string, T> Members
		{
			get
			{
				return members;
			}
		}

		/// <summary>
		/// Add the specified member.
		/// </summary>
		/// <param name="name">Name.</param>
		/// <param name="member">Member.</param>
		public bool Add(string name, T member)
		{
			var result = false;

			if (string.IsNullOrEmpty(name))
				throw new ArgumentException("Key is Empty (or nothing)!");

			lock (members)
				if (!Exist(name))
				{
					members.Add(name, member);
					result = true;
				}

			return result;
		}

		/// <summary>
		/// Remove the specified member.
		/// </summary>
		/// <param name="name">Name.</param>
		public void Remove(string name)
		{
			if (Exist(name))
				members.Remove(name);
		}

		public void HeartBeat()
		{
			foreach (var item in members.Values
				.Where(x => HasMethod(x.GetType(), "HeartBeat")))

				item.GetType().GetMethod("HeartBeat").Invoke(typeof(T), null);
		}

		/// <summary>
		/// Clear this instance.
		/// </summary>
		public void Clear()
		{
			members.Clear();
		}

		/// <summary>
		/// Exist the specified name.
		/// </summary>
		/// <param name="name">Name.</param>
		public bool Exist(string name) => members.ContainsKey(name);

		/// <summary>
		/// Determines whether this instance has method the specified name.
		/// </summary>
		/// <returns><c>true</c> if this instance has method the specified obj name; otherwise, <c>false</c>.</returns>
		/// <param name="obj">Object.</param>
		/// <param name="name">Name.</param>
		bool HasMethod(Type obj, string name) => obj.GetMethod(name) != null;

		/// <summary>
		/// Determines whether this instance has property the specified name.
		/// </summary>
		/// <returns><c>true</c> if this instance has property the specified obj name; otherwise, <c>false</c>.</returns>
		/// <param name="obj">Object.</param>
		/// <param name="name">Name.</param>
		public bool HasProperty(Type obj, string name) => obj.GetProperty(name) != null;

		/// <summary>
		/// Determines whether this instance has field the specified name.
		/// </summary>
		/// <returns><c>true</c> if this instance has field the specified obj name; otherwise, <c>false</c>.</returns>
		/// <param name="obj">Object.</param>
		/// <param name="name">Name.</param>
		public bool HasField(Type obj, string name) => obj.GetField(name) != null;

		Y GetPropertyValue<Y>(Type obj, string name)
		{
			var p = obj.GetProperty(name);

			return (Y)Convert.ChangeType(p.GetValue(p.PropertyType), p.PropertyType);
		}

		/// <summary>
		/// Gets the Number of Key/Value pairs in this Dictionary. 
		/// </summary>
		public int Count
		{
			get
			{
				return members.Count;
			}
		}
	}
}
