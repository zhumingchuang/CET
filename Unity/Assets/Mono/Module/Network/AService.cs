using System;
using System.IO;
using System.Net;

namespace ET
{
    public abstract class AService: IDisposable
    {
        /// <summary>
        /// 服务类型
        /// </summary>
        public ServiceType ServiceType { get; protected set; }

        /// <summary>
        /// 线程同步队列
        /// </summary>
        public ThreadSynchronizationContext ThreadSynchronizationContext;
        
        // localConn放在低32bit
        private long connectIdGenerater = int.MaxValue;
        public long CreateConnectChannelId(uint localConn)
        {
            return (--this.connectIdGenerater << 32) | localConn;
        }
        
        public uint CreateRandomLocalConn()
        {
            return (1u << 30) | RandomHelper.RandUInt32();
        }

        // localConn放在低32bit
        private long acceptIdGenerater = 1;
        /// <summary>
        /// 创建接受通道ID
        /// </summary>
        /// <param name="localConn"></param>
        /// <returns></returns>
        public long CreateAcceptChannelId(uint localConn)
        {
            return (++this.acceptIdGenerater << 32) | localConn;
        }



        public abstract void Update();

        /// <summary>
        /// 删除通道
        /// </summary>
        /// <param name="id"></param>
        public abstract void Remove(long id);
        
        public abstract bool IsDispose();

        /// <summary>
        /// 创建通道
        /// </summary>
        /// <param name="id"></param>
        /// <param name="address"></param>
        protected abstract void Get(long id, IPEndPoint address);

        public abstract void Dispose();

        protected abstract void Send(long channelId, long actorId, MemoryStream stream);

        /// <summary>
        /// 接受连接事件
        /// </summary>
        /// <param name="channelId"></param>
        /// <param name="ipEndPoint"></param>
        protected void OnAccept(long channelId, IPEndPoint ipEndPoint)
        {
            this.AcceptCallback.Invoke(channelId, ipEndPoint);
        }

        public void OnRead(long channelId, MemoryStream memoryStream)
        {
            this.ReadCallback.Invoke(channelId, memoryStream);
        }

        public void OnError(long channelId, int e)
        {
            this.Remove(channelId);
            
            this.ErrorCallback?.Invoke(channelId, e);
        }

        
        /// <summary>
        /// 接受连接事件
        /// </summary>
        public Action<long, IPEndPoint> AcceptCallback;
        public Action<long, int> ErrorCallback;
        public Action<long, MemoryStream> ReadCallback;

        public void Destroy()
        {
            this.Dispose();
        }

        /// <summary>
        /// 删除通道
        /// </summary>
        /// <param name="channelId"></param>
        public void RemoveChannel(long channelId)
        {
            this.Remove(channelId);
        }

        public void SendStream(long channelId, long actorId, MemoryStream stream)
        {
            this.Send(channelId, actorId, stream);
        }

        /// <summary>
        /// 创建通道
        /// </summary>
        /// <param name="id"></param>
        /// <param name="address"></param>
        public void GetOrCreate(long id, IPEndPoint address)
        {
            this.Get(id, address);
        }
    }
}