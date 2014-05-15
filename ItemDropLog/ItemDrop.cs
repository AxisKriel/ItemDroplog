using System;
namespace ItemDropLog
{
	public class ItemDrop
	{
		public DateTime CreationTime
		{
			get;
			private set;
		}
		public string SourceName
		{
			get;
			set;
		}
		public int NetworkId
		{
			get;
			set;
		}
		public int Stack
		{
			get;
			set;
		}
		public byte Prefix
		{
			get;
			set;
		}
		public float DropX
		{
			get;
			set;
		}
		public float DropY
		{
			get;
			set;
		}
		public ItemDrop(string sourceName, int networkId, int stack, int prefix, float dropX, float dropY)
		{
			this.CreationTime = DateTime.Now;
			this.SourceName = sourceName;
			this.NetworkId = networkId;
			this.Stack = stack;
			this.Prefix = (byte)prefix;
			this.DropX = dropX;
			this.DropY = dropY;
		}
	}
}
