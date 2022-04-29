using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;

namespace ET
{
	/// <summary>
	/// 主要时用来管理各个通道  通道包含主动发送连接 也包含 作为服务器 建立连接的通道
	/// 
	/// 
	/// </summary>
	public sealed class TService : AService
	{
		//用于管理各个Channel
		private readonly Dictionary<long, TChannel> idChannels = new Dictionary<long, TChannel>();

		//socekt事件的处理类
		private readonly SocketAsyncEventArgs innArgs = new SocketAsyncEventArgs();

		//主要用于作为服务器端监听连接使用的socket
		private Socket acceptor;

		//一个保存有需要发送数据Channel的id
		public HashSet<long> NeedStartSend = new HashSet<long>();

		public TService(ThreadSynchronizationContext threadSynchronizationContext, ServiceType serviceType)
		{
			this.foreachAction = channelId =>
			{
				TChannel tChannel = this.Get(channelId);
				tChannel?.Update();
			};
			this.ServiceType = serviceType;
			this.ThreadSynchronizationContext = threadSynchronizationContext;
		}

		public TService(ThreadSynchronizationContext threadSynchronizationContext, IPEndPoint ipEndPoint, ServiceType serviceType)
		{
			this.foreachAction = channelId =>
			{
				TChannel tChannel = this.Get(channelId);
				tChannel?.Update();
			};
			
			this.ServiceType = serviceType;
			this.ThreadSynchronizationContext = threadSynchronizationContext;
			
			this.acceptor = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
			this.acceptor.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
			this.innArgs.Completed += this.OnComplete;
			this.acceptor.Bind(ipEndPoint);
			this.acceptor.Listen(1000);

            this.ThreadSynchronizationContext.PostNext(this.AcceptAsync);
        }

		private void OnComplete(object sender, SocketAsyncEventArgs e)
		{
			switch (e.LastOperation)
			{
				case SocketAsyncOperation.Accept:
					SocketError socketError = e.SocketError;
					Socket acceptSocket = e.AcceptSocket;
					Log.Debug(e.LastOperation + "接收到完成消息");
					this.ThreadSynchronizationContext.Post(()=>{this.OnAcceptComplete(socketError, acceptSocket);});
					break;
				default:
					throw new Exception($"socket error: {e.LastOperation}");
			}
		}

		#region 网络线程

		/// <summary>
		/// 接受连接并创建通道
		/// </summary>
		/// <param name="socketError"></param>
		/// <param name="acceptSocket"></param>
		private void OnAcceptComplete(SocketError socketError, Socket acceptSocket)
		{
			if (this.acceptor == null)
			{
				return;
			}

			if (socketError != SocketError.Success)
			{
				Log.Error($"accept error {socketError}");
				return;
			}

			try
			{
				//创建接受通道ID
				long id = this.CreateAcceptChannelId(0);
				//创建通道
				TChannel channel = new TChannel(id, acceptSocket, this);
				this.idChannels.Add(channel.Id, channel);
				long channelId = channel.Id;
				
				this.OnAccept(channelId, channel.RemoteAddress);
				Log.Debug($"通道ID：{channelId} ip:{channel.RemoteAddress} {GetHashCode()}");
			}
			catch (Exception exception)
			{
				Log.Error(exception);
			}		
			
			// 开始新的accept
			this.AcceptAsync();
		}
		

		/// <summary>
		/// 检查新的连接
		/// </summary>
		private void AcceptAsync()
		{
			this.innArgs.AcceptSocket = null;
			//异步接受连接事件  如果操作在等待则为true  如果操作同步完成则为false 事件将不会调用
			if (this.acceptor.AcceptAsync(this.innArgs))
			{
				return;
			}
			OnAcceptComplete(this.innArgs.SocketError, this.innArgs.AcceptSocket);
		}

		/// <summary>
		/// 创建通道
		/// </summary>
		/// <param name="ipEndPoint"></param>
		/// <param name="id"></param>
		/// <returns></returns>
		private TChannel Create(IPEndPoint ipEndPoint, long id)
		{
			TChannel channel = new TChannel(id, ipEndPoint, this);
			this.idChannels.Add(channel.Id, channel);
			return channel;
		}

		/// <summary>
		/// 检查通道ID 没有就创建
		/// </summary>
		/// <param name="id"></param>
		/// <param name="address"></param>
		protected override void Get(long id, IPEndPoint address)
		{
			if (this.idChannels.TryGetValue(id, out TChannel _))
			{
				return;
			}
			this.Create(address, id);
		}
		
		/// <summary>
		/// 根据通道ID获取通道
		/// </summary>
		/// <param name="id"></param>
		/// <returns></returns>
		private TChannel Get(long id)
		{
			TChannel channel = null;
			this.idChannels.TryGetValue(id, out channel);
			return channel;
		}
		
		public override void Dispose()
		{
			this.acceptor?.Close();
			this.acceptor = null;
			this.innArgs.Dispose();
			ThreadSynchronizationContext = null;
			
			foreach (long id in this.idChannels.Keys.ToArray())
			{
				TChannel channel = this.idChannels[id];
				channel.Dispose();
			}
			this.idChannels.Clear();
		}

		/// <summary>
		/// 根据通道ID删除通道
		/// </summary>
		/// <param name="id"></param>
		public override void Remove(long id)
		{
			if (this.idChannels.TryGetValue(id, out TChannel channel))
			{
				channel.Dispose();	
			}

			this.idChannels.Remove(id);
		}

		protected override void Send(long channelId, long actorId, MemoryStream stream)
		{
			try
			{
				TChannel aChannel = this.Get(channelId);
				if (aChannel == null)
				{
					this.OnError(channelId, ErrorCore.ERR_SendMessageNotFoundTChannel);
					return;
				}
				aChannel.Send(actorId, stream);
			}
			catch (Exception e)
			{
				Log.Error(e);
			}
		}

		private readonly Action<long> foreachAction;

		/// <summary>
		/// 刷新需要发送数据的通道
		/// </summary>
		public override void Update()
		{
			this.NeedStartSend.Foreach(this.foreachAction);
			this.NeedStartSend.Clear();
		}
		
		public override bool IsDispose()
		{
			return this.ThreadSynchronizationContext == null;
		}
		
#endregion
		
	}
}