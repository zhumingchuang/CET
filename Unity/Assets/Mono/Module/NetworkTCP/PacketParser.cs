using System;
using System.IO;

namespace ET
{
	public enum ParserState
	{
		PacketSize,
		PacketBody
	}

	/// <summary>
	/// 数据解析
	/// 将CircularBuffer，与MemoryStream类关联起来
	/// </summary>
	public class PacketParser
	{
		private readonly CircularBuffer buffer;
		private int packetSize;
		private ParserState state;
		public AService service;
		private readonly byte[] cache = new byte[8];
		public const int InnerPacketSizeLength = 4;
		public const int OuterPacketSizeLength = 2;
		public MemoryStream MemoryStream;

		public PacketParser(CircularBuffer buffer, AService service)
		{
			this.buffer = buffer;
			this.service = service;
		}

		/// <summary>
		/// 确保将Buffer里面的数据以规定的规格读取到MemoryStream
		/// </summary>
		/// <returns></returns>
		/// <exception cref="Exception"></exception>
		/// <exception cref="ArgumentOutOfRangeException"></exception>
		/// 如果是数据大小阶段，则首先通过buffer长度，与packetSizeLength比较，确定数据是否处理完毕
		/// 未处理完毕，则从buffer取出规定长度的字节，再根据设定的包体大小字节数，将其转换成对应的ToInt32，
		/// ToUInt16值存放到packetSize中，将这个值作为后续包体大小的值，然后与设置好的值进行比较，
		/// 通过了设置状态，从而进行下一步处理
		/// PacketBody阶段，判定buffer剩余长度与解出来的packetSize进行比较，小于它，则认为已经处理完毕
		/// 如果不小于他，则正常流程，就是将buffer内，长度为packetSize的内容，读取到memoryStream中，然后处理完毕
		/// 可以保证黏包问题，有多余的数据，不会进入到memoryStream影响后续的数据处理
		public bool Parse()
		{
			while (true)
			{
				switch (this.state)
				{
					//确认消息数据大小阶段
					case ParserState.PacketSize:
					{
						if (this.service.ServiceType == ServiceType.Inner)
						{
							if (this.buffer.Length < InnerPacketSizeLength)
							{
								return false;
							}

							this.buffer.Read(this.cache, 0, InnerPacketSizeLength);

							this.packetSize = BitConverter.ToInt32(this.cache, 0);
							if (this.packetSize > ushort.MaxValue * 16 || this.packetSize < Packet.MinPacketSize)
							{
								throw new Exception($"recv packet size error, 可能是外网探测端口: {this.packetSize}");
							}
						}
						else
						{
							if (this.buffer.Length < OuterPacketSizeLength)
							{
								return false;
							}

							this.buffer.Read(this.cache, 0, OuterPacketSizeLength);

							this.packetSize = BitConverter.ToUInt16(this.cache, 0);
							if (this.packetSize < Packet.MinPacketSize)
							{
								throw new Exception($"recv packet size error, 可能是外网探测端口: {this.packetSize}");
							}
						}

						this.state = ParserState.PacketBody;
						break;
					}
					//处理消息数据本身阶段
					case ParserState.PacketBody:
					{
						if (this.buffer.Length < this.packetSize)
						{
							return false;
						}

						MemoryStream memoryStream = new MemoryStream(this.packetSize);
						this.buffer.Read(memoryStream, this.packetSize);
						//memoryStream.SetLength(this.packetSize - Packet.MessageIndex);
						this.MemoryStream = memoryStream;

						if (this.service.ServiceType == ServiceType.Inner)
						{
							memoryStream.Seek(Packet.MessageIndex, SeekOrigin.Begin);
						}
						else
						{
							memoryStream.Seek(Packet.OpcodeLength, SeekOrigin.Begin);
						}

						this.state = ParserState.PacketSize;
						return true;
					}
					default:
						throw new ArgumentOutOfRangeException();
				}
			}
		}
	}
}