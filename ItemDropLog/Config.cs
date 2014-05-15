using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
namespace ItemDropLog
{
	[JsonObject]
	public sealed class Config
	{
		private static Config instance;
		public static Config Instance
		{
			get
			{
				return Config.instance;
			}
		}
		[JsonProperty("ignoredItems")]
		public IList<string> IgnoredItems
		{
			get;
			set;
		}
		public Config()
		{
			this.IgnoredItems = new List<string>
			{
				"Copper Coin",
				"Silver Coin",
				"Gold Coin",
				"Heart",
				"Candy Apple",
				"Candy Cane",
				"Mana Star",
				"Soul Cake",
				"Sugar Plum"
			};
		}
		public static void CreateInstance(string path)
		{
			if (string.IsNullOrEmpty(path) || !File.Exists(path))
			{
				Config.instance = new Config();
				return;
			}
			using (Stream stream = File.OpenRead(path))
			{
				using (StreamReader streamReader = new StreamReader(stream))
				{
					Config.instance = JsonConvert.DeserializeObject<Config>(streamReader.ReadToEnd());
				}
			}
		}
		public static void SaveInstance(string path)
		{
			if (Config.instance == null)
			{
				return;
			}
			using (Stream stream = File.Create(path))
			{
				using (StreamWriter streamWriter = new StreamWriter(stream))
				{
					string value = JsonConvert.SerializeObject(Config.instance, 1);
					streamWriter.WriteLine(value);
				}
			}
		}
	}
}
