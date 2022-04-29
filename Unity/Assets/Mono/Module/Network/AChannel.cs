using System;
using System.IO;
using System.Net;

namespace ET
{
	/// <summary>
	/// channel 连接类型
	/// </summary>
	public enum ChannelType
	{
		/// <summary>
		/// 连接
		/// 以客户端方式发起连接
		/// </summary>
		Connect,

		/// <summary>
		/// 接受
		/// 作为服务器等待连接
		/// </summary>
		Accept,
	}

	public struct Packet
	{
		public const int MinPacketSize = 2;
		public const int OpcodeIndex = 8;
		public const int KcpOpcodeIndex = 0;
		public const int OpcodeLength = 2;
		public const int ActorIdIndex = 0;
		public const int ActorIdLength = 8;
		public const int MessageIndex = 10;

		public ushort Opcode;
		public long ActorId;
		public MemoryStream MemoryStream;
	}

	public abstract class AChannel: IDisposable
	{
		/// <summary>
		/// 通道ID
		/// </summary>
		public long Id;
		
		/// <summary>
		/// 连接类型
		/// </summary>
		public ChannelType ChannelType { get; protected set; }

		public int Error { get; set; }
		
		/// <summary>
		/// 连接地址
		/// </summary>
		public IPEndPoint RemoteAddress { get; set; }

		
		public bool IsDisposed
		{
			get
			{
				return this.Id == 0;	
			}
		}

		public abstract void Dispose();
	}
}