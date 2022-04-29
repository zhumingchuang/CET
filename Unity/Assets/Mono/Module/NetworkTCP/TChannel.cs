using System;
using System.IO;
using System.Net;
using System.Net.Sockets;

namespace ET
{
	/// <summary>
	/// 封装Socket,将回调push到主线程处理
	/// </summary>
	public sealed class TChannel: AChannel
	{
		private readonly TService Service;

		//基本的套接字功能类
		private Socket socket;

		//用于监听接收数据的事件
		private SocketAsyncEventArgs innArgs = new SocketAsyncEventArgs();

		//用户监听发送事件
		//用于主动发起连接时的socket事件监听
		private SocketAsyncEventArgs outArgs = new SocketAsyncEventArgs();

		//用于处理接收数据的buffer
		private readonly CircularBuffer recvBuffer = new CircularBuffer();

		//用于发送套接字数据
		private readonly CircularBuffer sendBuffer = new CircularBuffer();

		/// <summary>
		/// 是否在发送
		/// </summary>
		private bool isSending;

		/// <summary>
		/// 是否连接
		/// </summary>
		private bool isConnected;

		/// <summary>
		/// 数据包解析
		/// </summary>
		private readonly PacketParser parser;

		private readonly byte[] sendCache = new byte[Packet.OpcodeLength + Packet.ActorIdLength];

		private void OnComplete(object sender, SocketAsyncEventArgs e)
		{
			switch (e.LastOperation)
			{
				//连接完成
				case SocketAsyncOperation.Connect:
					this.Service.ThreadSynchronizationContext.Post(()=>OnConnectComplete(e));
					break;
			    //接收数据
				case SocketAsyncOperation.Receive:
					this.Service.ThreadSynchronizationContext.Post(()=>OnRecvComplete(e));
					break;
				//发送状态
				case SocketAsyncOperation.Send:
					this.Service.ThreadSynchronizationContext.Post(()=>OnSendComplete(e));
					break;
				//断开连接
				case SocketAsyncOperation.Disconnect:
					this.Service.ThreadSynchronizationContext.Post(()=>OnDisconnectComplete(e));
					break;
				default:
					throw new Exception($"socket error: {e.LastOperation}");
			}
		}

#region 网络线程
		
		/// <summary>
		/// 通道构造函数 用来主动连接
		/// </summary>
		/// <param name="id"></param>
		/// <param name="ipEndPoint"></param>
		/// <param name="service"></param>
		public TChannel(long id, IPEndPoint ipEndPoint, TService service)
		{
			this.ChannelType = ChannelType.Connect;
			this.Id = id;
			this.Service = service;
			this.socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
			this.socket.NoDelay = true;
			this.parser = new PacketParser(this.recvBuffer, this.Service);
			this.innArgs.Completed += this.OnComplete;
			this.outArgs.Completed += this.OnComplete;

			this.RemoteAddress = ipEndPoint;
			this.isConnected = false;
			this.isSending = false;

			this.Service.ThreadSynchronizationContext.PostNext(this.ConnectAsync);
		}

		/// <summary>
		/// 连接通道 服务器构造函数
		/// </summary>
		/// <param name="id">连接通道ID</param>
		/// <param name="socket">服务器socket</param>
		/// <param name="service"></param>
		public TChannel(long id, Socket socket, TService service)
		{
			this.ChannelType = ChannelType.Accept;
			this.Id = id;
			this.Service = service;
			this.socket = socket;
			this.socket.NoDelay = true;
			this.parser = new PacketParser(this.recvBuffer, this.Service);
			this.innArgs.Completed += this.OnComplete;
			this.outArgs.Completed += this.OnComplete;

			this.RemoteAddress = (IPEndPoint)socket.RemoteEndPoint;
			this.isConnected = true;
			this.isSending = false;
			
			// 下一帧再开始读写
			this.Service.ThreadSynchronizationContext.PostNext(() =>
			{
				this.StartRecv();
				this.StartSend();
			});
		}
		
		

		public override void Dispose()
		{
			if (this.IsDisposed)
			{
				return;
			}

			Log.Info($"channel dispose: {this.Id} {this.RemoteAddress}");
			
			long id = this.Id;
			this.Id = 0;
			this.Service.Remove(id);
			this.socket.Close();
			this.innArgs.Dispose();
			this.outArgs.Dispose();
			this.innArgs = null;
			this.outArgs = null;
			this.socket = null;
		}

		/// <summary>
		/// 发送数据
		/// </summary>
		/// <param name="actorId"></param>
		/// <param name="stream"></param>
		/// <exception cref="Exception"></exception>
		public void Send(long actorId, MemoryStream stream)
		{
			if (this.IsDisposed)
			{
				throw new Exception("TChannel已经被Dispose, 不能发送消息");
			}

			switch (this.Service.ServiceType)
			{
				case ServiceType.Inner:
				{
					int messageSize = (int) (stream.Length - stream.Position);
					if (messageSize > ushort.MaxValue * 16)
					{
						throw new Exception($"send packet too large: {stream.Length} {stream.Position}");
					}

					this.sendCache.WriteTo(0, messageSize);
					this.sendBuffer.Write(this.sendCache, 0, PacketParser.InnerPacketSizeLength);

					// actorId
					stream.GetBuffer().WriteTo(0, actorId);
					this.sendBuffer.Write(stream.GetBuffer(), (int)stream.Position, (int)(stream.Length - stream.Position));
					break;
				}
				case ServiceType.Outer:
				{
					stream.Seek(Packet.ActorIdLength, SeekOrigin.Begin); // 外网不需要actorId
					ushort messageSize = (ushort) (stream.Length - stream.Position);

					this.sendCache.WriteTo(0, messageSize);
					this.sendBuffer.Write(this.sendCache, 0, PacketParser.OuterPacketSizeLength);
					
					this.sendBuffer.Write(stream.GetBuffer(), (int)stream.Position, (int)(stream.Length - stream.Position));
					break;
				}
			}
			
			//如果当前没有在发送数据 把通道ID存在列表中等待发送
			if (!this.isSending)
			{
				//this.StartSend();
				this.Service.NeedStartSend.Add(this.Id);
			}
		}

		/// <summary>
		/// 异步连接
		/// </summary>
		private void ConnectAsync()
		{
			this.outArgs.RemoteEndPoint = this.RemoteAddress;
			//异步连接
			if (this.socket.ConnectAsync(this.outArgs))
			{
				return;
			}
			OnConnectComplete(this.outArgs);
		}

		/// <summary>
		/// 连接完成
		/// </summary>
		/// <param name="o"></param>
		private void OnConnectComplete(object o)
		{
			if (this.socket == null)
			{
				return;
			}
			SocketAsyncEventArgs e = (SocketAsyncEventArgs) o;
			
			if (e.SocketError != SocketError.Success)
			{
				this.OnError((int)e.SocketError);	
				return;
			}

			e.RemoteEndPoint = null;
			this.isConnected = true;
			this.StartRecv();
			this.StartSend();
		}

		private void OnDisconnectComplete(object o)
		{
			SocketAsyncEventArgs e = (SocketAsyncEventArgs)o;
			this.OnError((int)e.SocketError);
		}

		/// <summary>
		/// 开始接收
		/// </summary>
		private void StartRecv()
		{
			while (true)
			{
				try
				{
					if (this.socket == null)
					{
						return;
					}
					
					int size = this.recvBuffer.ChunkSize - this.recvBuffer.LastIndex;
					this.innArgs.SetBuffer(this.recvBuffer.Last, this.recvBuffer.LastIndex, size);
				}
				catch (Exception e)
				{
					Log.Error($"tchannel error: {this.Id}\n{e}");
					this.OnError(ErrorCore.ERR_TChannelRecvError);
					return;
				}
			
				//异步接收数据
				if (this.socket.ReceiveAsync(this.innArgs))
				{
					return;
				}
				this.HandleRecv(this.innArgs);
			}
		}

		/// <summary>
		/// 异步接收数据事件
		/// </summary>
		/// <param name="o"></param>
		private void OnRecvComplete(object o)
		{
			this.HandleRecv(o);
			
			if (this.socket == null)
			{
				return;
			}
			this.StartRecv();
		}

		/// <summary>
		/// 处理接收数据
		/// </summary>
		/// <param name="o"></param>
		private void HandleRecv(object o)
		{
			if (this.socket == null)
			{
				return;
			}
			SocketAsyncEventArgs e = (SocketAsyncEventArgs) o;

			if (e.SocketError != SocketError.Success)
			{
				this.OnError((int)e.SocketError);
				return;
			}

			if (e.BytesTransferred == 0)
			{
				this.OnError(ErrorCore.ERR_PeerDisconnect);
				return;
			}

			this.recvBuffer.LastIndex += e.BytesTransferred;
			if (this.recvBuffer.LastIndex == this.recvBuffer.ChunkSize)
			{
				this.recvBuffer.AddLast();
				this.recvBuffer.LastIndex = 0;
			}

			// 收到消息回调
			while (true)
			{
				// 这里循环解析消息执行，有可能，执行消息的过程中断开了session
				if (this.socket == null)
				{
					return;
				}
				try
				{
					bool ret = this.parser.Parse();
					if (!ret)
					{
						break;
					}
					
					this.OnRead(this.parser.MemoryStream);
				}
				catch (Exception ee)
				{
					Log.Error($"ip: {this.RemoteAddress} {ee}");
					this.OnError(ErrorCore.ERR_SocketError);
					return;
				}
			}
		}

		/// <summary>
		/// 刷新需要发送的数据
		/// </summary>
		public void Update()
		{
			this.StartSend();
		}

		/// <summary>
		/// 开始发送
		/// </summary>
		/// <exception cref="Exception"></exception>
		private void StartSend()
		{
			if(!this.isConnected)
			{
				return;
			}

			if (this.isSending)
			{
				return;
			}
			
			while (true)
			{
				try
				{
					if (this.socket == null)
					{
						this.isSending = false;
						return;
					}
					
					// 没有数据需要发送
					if (this.sendBuffer.Length == 0)
					{
						this.isSending = false;
						return;
					}

					this.isSending = true;

					int sendSize = this.sendBuffer.ChunkSize - this.sendBuffer.FirstIndex;
					if (sendSize > this.sendBuffer.Length)
					{
						sendSize = (int)this.sendBuffer.Length;
					}
					
					this.outArgs.SetBuffer(this.sendBuffer.First, this.sendBuffer.FirstIndex, sendSize);
					
					//异步发送数据
					if (this.socket.SendAsync(this.outArgs))
					{
						return;
					}
				
					HandleSend(this.outArgs);
				}
				catch (Exception e)
				{
					throw new Exception($"socket set buffer error: {this.sendBuffer.First.Length}, {this.sendBuffer.FirstIndex}", e);
				}
			}
		}

		/// <summary>
		/// 发送事件
		/// </summary>
		/// <param name="o"></param>
		private void OnSendComplete(object o)
		{
			HandleSend(o);
			
			this.isSending = false;
			
			this.StartSend();
		}

		/// <summary>
		/// 处理发送数据
		/// </summary>
		/// <param name="o"></param>
		private void HandleSend(object o)
		{
			if (this.socket == null)
			{
				return;
			}
			
			SocketAsyncEventArgs e = (SocketAsyncEventArgs) o;

			if (e.SocketError != SocketError.Success)
			{
				this.OnError((int)e.SocketError);
				return;
			}
			
			if (e.BytesTransferred == 0)
			{
				this.OnError(ErrorCore.ERR_PeerDisconnect);
				return;
			}
			
			this.sendBuffer.FirstIndex += e.BytesTransferred;
			if (this.sendBuffer.FirstIndex == this.sendBuffer.ChunkSize)
			{
				this.sendBuffer.FirstIndex = 0;
				this.sendBuffer.RemoveFirst();
			}
		}
		
		/// <summary>
		/// 读取接收数据事件
		/// </summary>
		/// <param name="memoryStream"></param>
		private void OnRead(MemoryStream memoryStream)
		{
			try
			{
				long channelId = this.Id;
				this.Service.OnRead(channelId, memoryStream);
			}
			catch (Exception e)
			{
				Log.Error($"{this.RemoteAddress} {memoryStream.Length} {e}");
				// 出现任何消息解析异常都要断开Session，防止客户端伪造消息
				this.OnError(ErrorCore.ERR_PacketParserError);
			}
		}

		private void OnError(int error)
		{
			Log.Info($"TChannel OnError: {error} {this.RemoteAddress}");
			
			long channelId = this.Id;
			
			this.Service.Remove(channelId);
			
			this.Service.OnError(channelId, error);
		}

#endregion

	}
}