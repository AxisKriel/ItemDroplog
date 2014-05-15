using Mono.Data.Sqlite;
using MySql.Data.MySqlClient;
using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Text;
using Terraria;
using TerrariaApi.Server;
using TShockAPI;
using TShockAPI.DB;
namespace ItemDropLog
{
	[ApiVersion(1, 15)]
	public class ItemDropLogPlugin : TerrariaPlugin
	{
		private static readonly string ConfigPath = Path.Combine(TShock.SavePath, "itemdroplog.json");
		private object _dropLocker;
		private ItemDrop[] _drops;
		private object _pendingLocker;
		private IList<ItemDrop> _playerDropsPending;
		private IList<Item> _ignoredItems;
		public override string Author
		{
			get
			{
				return "IcyTerraria";
			}
		}
		public override string Name
		{
			get
			{
				return "Item Drop Logger";
			}
		}
		public override string Description
		{
			get
			{
				return "Item Drop Logger Plugin";
			}
		}
		public override Version Version
		{
			get
			{
				return new Version(0, 7, 4);
			}
		}
		public string SavePath
		{
			get
			{
				return TShock.SavePath;
			}
		}
		internal IDbConnection Database
		{
			get;
			set;
		}
		public ItemDropLogPlugin(Main game) : base(game)
		{
			this._dropLocker = new object();
			this._drops = new ItemDrop[Main.item.Length];
			this._pendingLocker = new object();
			this._playerDropsPending = new List<ItemDrop>(Main.item.Length);
			this._ignoredItems = new List<Item>();
		}
		public override void Initialize()
		{
			ServerApi.get_Hooks().get_GameInitialize().Register(this, new HookHandler<EventArgs>(this.OnInitialize));
			ServerApi.get_Hooks().get_GamePostInitialize().Register(this, new HookHandler<EventArgs>(this.OnPostInitialize));
			ServerApi.get_Hooks().get_NetGetData().Register(this, new HookHandler<GetDataEventArgs>(this.OnGetData));
			ServerApi.get_Hooks().get_NetSendData().Register(this, new HookHandler<SendDataEventArgs>(this.OnSendData));
		}
		protected override void Dispose(bool disposing)
		{
			if (disposing)
			{
				ServerApi.get_Hooks().get_GameInitialize().Deregister(this, new HookHandler<EventArgs>(this.OnInitialize));
				ServerApi.get_Hooks().get_GamePostInitialize().Deregister(this, new HookHandler<EventArgs>(this.OnPostInitialize));
				ServerApi.get_Hooks().get_NetGetData().Deregister(this, new HookHandler<GetDataEventArgs>(this.OnGetData));
				ServerApi.get_Hooks().get_NetSendData().Deregister(this, new HookHandler<SendDataEventArgs>(this.OnSendData));
			}
			base.Dispose(disposing);
		}
		private void OnInitialize(EventArgs args)
		{
			Commands.ChatCommands.Add(new Command("playeritemhistory.search", new CommandDelegate(this.PlayerItemHistoryReceive), new string[]
			{
				"pihr"
			}));
			Commands.ChatCommands.Add(new Command("playeritemhistory.search", new CommandDelegate(this.PlayerItemHistoryGive), new string[]
			{
				"pihg"
			}));
			List<Command> arg_A0_0 = Commands.ChatCommands;
			Command command = new Command("playeritemhistory.reload", new CommandDelegate(this.PlayerItemHistoryReload), new string[]
			{
				"pihreload"
			});
			command.set_AllowServer(true);
			arg_A0_0.Add(command);
			List<Command> arg_DF_0 = Commands.ChatCommands;
			Command command2 = new Command("playeritemhistory.flush", new CommandDelegate(this.PlayerItemHistoryFlush), new string[]
			{
				"pihflush"
			});
			command2.set_AllowServer(true);
			arg_DF_0.Add(command2);
			string a;
			if ((a = TShock.get_Config().StorageType.ToLowerInvariant()) != null)
			{
				if (!(a == "mysql"))
				{
					if (a == "sqlite")
					{
						if (!Directory.Exists(this.SavePath))
						{
							Directory.CreateDirectory(this.SavePath);
						}
						string arg = Path.Combine(this.SavePath, "itemlog.sqlite");
						this.Database = new SqliteConnection(string.Format("uri=file://{0},Version=3", arg));
					}
				}
				else
				{
					string[] array = TShock.get_Config().MySqlHost.Split(new char[]
					{
						':'
					});
					MySqlConnectionStringBuilder mySqlConnectionStringBuilder = new MySqlConnectionStringBuilder();
					mySqlConnectionStringBuilder.set_Server(array[0]);
					mySqlConnectionStringBuilder.set_Port((array.Length == 1) ? 3306u : uint.Parse(array[1]));
					mySqlConnectionStringBuilder.set_Database(TShock.get_Config().MySqlDbName);
					mySqlConnectionStringBuilder.set_UserID(TShock.get_Config().MySqlUsername);
					mySqlConnectionStringBuilder.set_Password(TShock.get_Config().MySqlPassword);
					MySqlConnectionStringBuilder mySqlConnectionStringBuilder2 = mySqlConnectionStringBuilder;
					this.Database = new MySqlConnection(mySqlConnectionStringBuilder2.ToString());
				}
			}
			IQueryBuilder queryBuilder = null;
			if (this.Database is SqliteConnection)
			{
				queryBuilder = new SqliteQueryCreator();
			}
			else if (this.Database is MySqlConnection)
			{
				queryBuilder = new MysqlQueryCreator();
			}
			if (queryBuilder == null)
			{
				Log.ConsoleError("Unknown database type!");
				return;
			}
			SqlTableCreator sqlTableCreator = new SqlTableCreator(this.Database, queryBuilder);
			SqlTableCreator arg_401_0 = sqlTableCreator;
			string arg_3FC_0 = "ItemLog";
			SqlColumn[] array2 = new SqlColumn[14];
			SqlColumn[] arg_26B_0 = array2;
			int arg_26B_1 = 0;
			SqlColumn sqlColumn = new SqlColumn("Id", 3);
			sqlColumn.set_Primary(true);
			sqlColumn.set_AutoIncrement(true);
			arg_26B_0[arg_26B_1] = sqlColumn;
			SqlColumn[] arg_290_0 = array2;
			int arg_290_1 = 1;
			SqlColumn sqlColumn2 = new SqlColumn("Timestamp", 253);
			sqlColumn2.set_Length(new int?(19));
			arg_290_0[arg_290_1] = sqlColumn2;
			SqlColumn[] arg_2B5_0 = array2;
			int arg_2B5_1 = 2;
			SqlColumn sqlColumn3 = new SqlColumn("ServerName", 253);
			sqlColumn3.set_Length(new int?(64));
			arg_2B5_0[arg_2B5_1] = sqlColumn3;
			SqlColumn[] arg_2DA_0 = array2;
			int arg_2DA_1 = 3;
			SqlColumn sqlColumn4 = new SqlColumn("SourcePlayerName", 253);
			sqlColumn4.set_Length(new int?(30));
			arg_2DA_0[arg_2DA_1] = sqlColumn4;
			SqlColumn[] arg_2FF_0 = array2;
			int arg_2FF_1 = 4;
			SqlColumn sqlColumn5 = new SqlColumn("SourceIP", 253);
			sqlColumn5.set_Length(new int?(16));
			arg_2FF_0[arg_2FF_1] = sqlColumn5;
			SqlColumn[] arg_324_0 = array2;
			int arg_324_1 = 5;
			SqlColumn sqlColumn6 = new SqlColumn("TargetPlayerName", 253);
			sqlColumn6.set_Length(new int?(30));
			arg_324_0[arg_324_1] = sqlColumn6;
			SqlColumn[] arg_349_0 = array2;
			int arg_349_1 = 6;
			SqlColumn sqlColumn7 = new SqlColumn("TargetIP", 253);
			sqlColumn7.set_Length(new int?(16));
			arg_349_0[arg_349_1] = sqlColumn7;
			SqlColumn[] arg_36E_0 = array2;
			int arg_36E_1 = 7;
			SqlColumn sqlColumn8 = new SqlColumn("Action", 253);
			sqlColumn8.set_Length(new int?(16));
			arg_36E_0[arg_36E_1] = sqlColumn8;
			array2[8] = new SqlColumn("DropX", 4);
			array2[9] = new SqlColumn("DropY", 4);
			array2[10] = new SqlColumn("ItemNetId", 3);
			SqlColumn[] arg_3C3_0 = array2;
			int arg_3C3_1 = 11;
			SqlColumn sqlColumn9 = new SqlColumn("ItemName", 253);
			sqlColumn9.set_Length(new int?(70));
			arg_3C3_0[arg_3C3_1] = sqlColumn9;
			array2[12] = new SqlColumn("ItemStack", 3);
			SqlColumn[] arg_3F9_0 = array2;
			int arg_3F9_1 = 13;
			SqlColumn sqlColumn10 = new SqlColumn("ItemPrefix", 253);
			sqlColumn10.set_Length(new int?(16));
			arg_3F9_0[arg_3F9_1] = sqlColumn10;
			arg_401_0.EnsureExists(new SqlTable(arg_3FC_0, array2));
			ItemDropLogger.Database = this.Database;
		}
		private void OnPostInitialize(EventArgs args)
		{
			this.SetupConfig();
		}
		private void OnGetData(GetDataEventArgs args)
		{
			if (args.get_MsgID() == 21)
			{
				TSPlayer tSPlayer = TShock.Players[args.get_Msg().whoAmI];
				using (MemoryStream memoryStream = new MemoryStream(args.get_Msg().readBuffer, args.get_Index(), args.get_Length()))
				{
					using (BinaryReader binaryReader = new BinaryReader(memoryStream, Encoding.UTF8, true))
					{
						int num = (int)binaryReader.ReadInt16();
						float num2 = binaryReader.ReadSingle();
						float num3 = binaryReader.ReadSingle();
						binaryReader.ReadSingle();
						binaryReader.ReadSingle();
						int num4 = (int)binaryReader.ReadInt16();
						int num5 = (int)binaryReader.ReadByte();
						binaryReader.ReadBoolean();
						int num6 = (int)binaryReader.ReadInt16();
						if (num == 400)
						{
							Item itemById = TShock.Utils.GetItemById(num6);
							string name = tSPlayer.get_Name();
							string sourceIP = tSPlayer.get_IP().Split(new char[]
							{
								':'
							})[0];
							lock (this._pendingLocker)
							{
								float dropX = num2 / 16f;
								float dropY = num3 / 16f;
								this._playerDropsPending.Add(new ItemDrop(name, itemById.netID, num4, num5, dropX, dropY));
								if (this.CheckItem(itemById))
								{
									ItemDropLogger.CreateItemEntry(new ItemDropLogInfo("PlayerDrop", name, string.Empty, itemById.netID, num4, num5, dropX, dropY)
									{
										SourceIP = sourceIP
									});
								}
								goto IL_241;
							}
						}
						if (num < 400 && num6 == 0)
						{
							Item item = Main.item[num];
							if (item.netID != 0)
							{
								string name2 = tSPlayer.get_Name();
								string targetIP = tSPlayer.get_IP().Split(new char[]
								{
									':'
								})[0];
								lock (this._dropLocker)
								{
									ItemDrop itemDrop = this._drops[num];
									if (this._drops[num] != null && this._drops[num].NetworkId != 0)
									{
										if (this.CheckItem(item))
										{
											ItemDropLogger.UpdateItemEntry(new ItemDropLogInfo("Pickup", itemDrop.SourceName, name2, itemDrop.NetworkId, itemDrop.Stack, (int)itemDrop.Prefix)
											{
												TargetIP = targetIP
											});
										}
										this._drops[num] = null;
									}
								}
							}
						}
						IL_241:;
					}
				}
			}
		}
		private void OnSendData(SendDataEventArgs args)
		{
			if (args.get_MsgId() != 21)
			{
				return;
			}
			int number = args.get_number();
			if (this._playerDropsPending.Count > 0 && number < 400)
			{
				Item item = Main.item[number];
				ItemDrop itemDrop = this._playerDropsPending.FirstOrDefault((ItemDrop x) => x.NetworkId == item.netID && x.Stack == item.stack && x.Prefix == item.prefix);
				if (itemDrop != null)
				{
					lock (this._dropLocker)
					{
						this._drops[number] = itemDrop;
					}
					lock (this._pendingLocker)
					{
						this._playerDropsPending.Remove(itemDrop);
					}
				}
			}
		}
		private void PlayerItemHistoryReceive(CommandArgs args)
		{
			if (args.get_Parameters().Count == 0)
			{
				args.get_Player().SendErrorMessage("Invalid syntax! Proper syntax: /pihr <player> [page] [item id/name]");
				return;
			}
			string text = args.get_Parameters()[0];
			List<TSPlayer> list = TShock.Utils.FindPlayer(text);
			string text2;
			if (list.Count == 0)
			{
				using (QueryResult queryResult = DbExt.QueryReader(this.Database, "SELECT COUNT(*) AS `Count` FROM `ItemLog` WHERE `TargetPlayerName`=@0", new object[]
				{
					text
				}))
				{
					if (!queryResult.Read() || queryResult.Get<int>("Count") <= 0)
					{
						args.get_Player().SendErrorMessage("Invalid player!");
						return;
					}
				}
				text2 = text;
				goto IL_E3;
			}
			if (list.Count <= 1)
			{
				text2 = list[0].get_Name();
				goto IL_E3;
			}
			TShock.Utils.SendMultipleMatchError(args.get_Player(), 
				from p in list
				select p.get_Name());
			return;
			IL_E3:
			int num;
			if (args.get_Parameters().Count < 2 || !int.TryParse(args.get_Parameters()[1], out num) || num < 0)
			{
				num = 1;
			}
			Item item = null;
			if (args.get_Parameters().Count >= 3)
			{
				List<Item> itemByIdOrName = TShock.Utils.GetItemByIdOrName(args.get_Parameters()[2]);
				if (itemByIdOrName.Count == 0)
				{
					args.get_Player().SendErrorMessage("Invalid item!");
					return;
				}
				if (itemByIdOrName.Count > 1)
				{
					TShock.Utils.SendMultipleMatchError(args.get_Player(), 
						from x in itemByIdOrName
						select x.name);
					return;
				}
				item = itemByIdOrName[0];
			}
			QueryResult queryResult2;
			if (item != null)
			{
				queryResult2 = DbExt.QueryReader(this.Database, "SELECT * FROM `ItemLog` WHERE `TargetPlayerName`=@0 AND `ItemNetId`=@1 ORDER BY `Timestamp` DESC LIMIT @2,@3", new object[]
				{
					text2,
					item.netID,
					(num - 1) * 5,
					5
				});
			}
			else
			{
				queryResult2 = DbExt.QueryReader(this.Database, "SELECT * FROM `ItemLog` WHERE `TargetPlayerName`=@0 ORDER BY `Timestamp` DESC LIMIT @1,@2", new object[]
				{
					text2,
					(num - 1) * 5,
					5
				});
			}
			using (queryResult2)
			{
				args.get_Player().SendInfoMessage("Item Drop Log - v{0} - by IcyTerraria", new object[]
				{
					this.get_Version()
				});
				args.get_Player().SendInfoMessage("Results for {0}:", new object[]
				{
					text2
				});
				int num2 = (num - 1) * 5;
				DateTime now = DateTime.Now;
				while (queryResult2.Read())
				{
					Item itemById = TShock.Utils.GetItemById(queryResult2.Get<int>("ItemNetId"));
					string s = queryResult2.Get<string>("Timestamp");
					string text3 = queryResult2.Get<string>("ServerName");
					string text4 = queryResult2.Get<string>("SourcePlayerName");
					string text5 = queryResult2.Get<string>("TargetPlayerName");
					string value = queryResult2.Get<string>("ItemName");
					int num3 = queryResult2.Get<int>("ItemStack");
					string text6 = queryResult2.Get<string>("ItemPrefix");
					StringBuilder stringBuilder = new StringBuilder();
					if (text6 != "None")
					{
						stringBuilder.Append(text6).Append(' ');
					}
					stringBuilder.Append(value);
					if (itemById.maxStack > 1)
					{
						stringBuilder.Append(' ').AppendFormat("({0}/{1})", num3, itemById.maxStack);
					}
					string text7 = string.Empty;
					if (!string.IsNullOrEmpty(text3))
					{
						text7 = " on " + text3;
					}
					DateTime d = DateTime.Parse(s);
					TimeSpan span = now - d;
					args.get_Player().SendInfoMessage("{0}. {1} received {2} from {3}{4} ({5} ago)", new object[]
					{
						++num2,
						text5,
						stringBuilder.ToString(),
						text4,
						text7,
						this.TimeSpanToDurationString(span)
					});
				}
			}
		}
		private void PlayerItemHistoryGive(CommandArgs args)
		{
			if (args.get_Parameters().Count == 0)
			{
				args.get_Player().SendErrorMessage("Invalid syntax! Proper syntax: /pihg <player> [page] [item id/name]");
				return;
			}
			string text = args.get_Parameters()[0];
			List<TSPlayer> list = TShock.Utils.FindPlayer(text);
			string text2;
			if (list.Count == 0)
			{
				using (QueryResult queryResult = DbExt.QueryReader(this.Database, "SELECT COUNT(*) AS `Count` FROM `ItemLog` WHERE `SourcePlayerName`=@0", new object[]
				{
					text
				}))
				{
					if (!queryResult.Read() || queryResult.Get<int>("Count") <= 0)
					{
						args.get_Player().SendErrorMessage("Invalid player!");
						return;
					}
				}
				text2 = text;
				goto IL_E3;
			}
			if (list.Count <= 1)
			{
				text2 = list[0].get_Name();
				goto IL_E3;
			}
			TShock.Utils.SendMultipleMatchError(args.get_Player(), 
				from p in list
				select p.get_Name());
			return;
			IL_E3:
			int num;
			if (args.get_Parameters().Count < 2 || !int.TryParse(args.get_Parameters()[1], out num) || num < 0)
			{
				num = 1;
			}
			Item item = null;
			if (args.get_Parameters().Count >= 3)
			{
				List<Item> itemByIdOrName = TShock.Utils.GetItemByIdOrName(args.get_Parameters()[2]);
				if (itemByIdOrName.Count == 0)
				{
					args.get_Player().SendErrorMessage("Invalid item!");
					return;
				}
				if (itemByIdOrName.Count > 1)
				{
					TShock.Utils.SendMultipleMatchError(args.get_Player(), 
						from x in itemByIdOrName
						select x.name);
					return;
				}
				item = itemByIdOrName[0];
			}
			QueryResult queryResult2;
			if (item != null)
			{
				queryResult2 = DbExt.QueryReader(this.Database, "SELECT * FROM `ItemLog` WHERE `SourcePlayerName`=@0 AND `ItemNetId`=@1 ORDER BY `Timestamp` DESC LIMIT @2,@3", new object[]
				{
					text2,
					item.netID,
					(num - 1) * 5,
					5
				});
			}
			else
			{
				queryResult2 = DbExt.QueryReader(this.Database, "SELECT * FROM `ItemLog` WHERE `SourcePlayerName`=@0 ORDER BY `Timestamp` DESC LIMIT @1,@2", new object[]
				{
					text2,
					(num - 1) * 5,
					5
				});
			}
			using (queryResult2)
			{
				args.get_Player().SendInfoMessage("Item Drop Log - v{0} - by IcyTerraria", new object[]
				{
					this.get_Version()
				});
				args.get_Player().SendInfoMessage("Results for {0}:", new object[]
				{
					text2
				});
				int num2 = (num - 1) * 5;
				DateTime now = DateTime.Now;
				while (queryResult2.Read())
				{
					Item itemById = TShock.Utils.GetItemById(queryResult2.Get<int>("ItemNetId"));
					string s = queryResult2.Get<string>("Timestamp");
					string text3 = queryResult2.Get<string>("ServerName");
					string text4 = queryResult2.Get<string>("SourcePlayerName");
					string text5 = queryResult2.Get<string>("TargetPlayerName");
					string value = queryResult2.Get<string>("ItemName");
					int num3 = queryResult2.Get<int>("ItemStack");
					string text6 = queryResult2.Get<string>("ItemPrefix");
					StringBuilder stringBuilder = new StringBuilder();
					if (text6 != "None")
					{
						stringBuilder.Append(text6).Append(' ');
					}
					stringBuilder.Append(value);
					if (itemById.maxStack > 1)
					{
						stringBuilder.Append(' ').AppendFormat("({0}/{1})", num3, itemById.maxStack);
					}
					string text7 = string.Empty;
					if (!string.IsNullOrEmpty(text3))
					{
						text7 = " on " + text3;
					}
					DateTime d = DateTime.Parse(s);
					TimeSpan span = now - d;
					args.get_Player().SendInfoMessage("{0}. {1} gave {2} to {3}{4} ({5} ago)", new object[]
					{
						++num2,
						text4,
						stringBuilder.ToString(),
						text5,
						text7,
						this.TimeSpanToDurationString(span)
					});
				}
			}
		}
		private void PlayerItemHistoryReload(CommandArgs args)
		{
			this.LoadConfig(ItemDropLogPlugin.ConfigPath);
			args.get_Player().SendInfoMessage("PlayerItemHistory config reloaded.");
		}
		private void PlayerItemHistoryFlush(CommandArgs args)
		{
			if (args.get_Parameters().Count == 0)
			{
				args.get_Player().SendErrorMessage("Invalid syntax! Proper syntax: /pihflush <days>");
				return;
			}
			int num;
			if (!int.TryParse(args.get_Parameters()[0], out num) || num < 1)
			{
				args.get_Player().SendErrorMessage("Invalid days");
				return;
			}
			DateTime dateTime = DateTime.Now.AddDays((double)(-(double)num));
			int num2 = DbExt.Query(this.Database, "DELETE FROM `ItemLog` WHERE `Timestamp`<@0 AND `ServerName`=@1", new object[]
			{
				dateTime.ToString("s"),
				TShock.get_Config().ServerName
			});
			args.get_Player().SendInfoMessage("Successfully flushed {0:n0} rows from the database.", new object[]
			{
				num2
			});
		}
		private string TimeSpanToDurationString(TimeSpan span)
		{
			int days = span.Days;
			int hours = span.Hours;
			int minutes = span.Minutes;
			int seconds = span.Seconds;
			List<string> list = new List<string>(4);
			if (days > 0)
			{
				list.Add(days + "d");
			}
			if (hours > 0)
			{
				list.Add(hours + "h");
			}
			if (minutes > 0)
			{
				list.Add(minutes + "m");
			}
			if (seconds > 0)
			{
				list.Add(seconds + "s");
			}
			return string.Join(" ", list);
		}
		private string GetPrefixName(int pre)
		{
			string result = "None";
			if (pre > 0)
			{
				result = Lang.prefix[pre];
			}
			return result;
		}
		private void SetupConfig()
		{
			try
			{
				if (File.Exists(ItemDropLogPlugin.ConfigPath))
				{
					this.LoadConfig(ItemDropLogPlugin.ConfigPath);
				}
				else
				{
					Log.ConsoleError("ItemDropLog configuration not found. Using default configuration.");
					this.LoadConfig(null);
					Config.SaveInstance(ItemDropLogPlugin.ConfigPath);
				}
			}
			catch (Exception ex)
			{
				Log.ConsoleError(ex.ToString());
			}
		}
		private void LoadConfig(string path)
		{
			Config.CreateInstance(path);
			this._ignoredItems.Clear();
			foreach (string current in Config.Instance.IgnoredItems)
			{
				List<Item> itemByIdOrName = TShock.Utils.GetItemByIdOrName(current);
				if (itemByIdOrName.Count > 0)
				{
					this._ignoredItems.Add(itemByIdOrName[0]);
				}
			}
		}
		private bool CheckItem(Item item)
		{
			return this._ignoredItems.All((Item x) => x.netID != item.netID);
		}
	}
}
