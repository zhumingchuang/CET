using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;

namespace ET
{
    public struct ProcessActorId
    {
        public int Process;
        public long ActorId;

        public ProcessActorId(long actorId)
        {
            InstanceIdStruct instanceIdStruct = new InstanceIdStruct(actorId);
            this.Process = instanceIdStruct.Process;
            instanceIdStruct.Process = Game.Options.Process;
            this.ActorId = instanceIdStruct.ToLong();
        }
    }

    /// <summary>
    /// 服务器内部网络组件
    /// </summary>
    [ChildType(typeof(Session))]
    public class NetInnerComponent: Entity, IAwake<IPEndPoint, int>, IAwake<int>, IDestroy
    {
        public AService Service;

        public static NetInnerComponent Instance;

        /// <summary>
        /// 会话流调度类型  服务器内部
        /// </summary>
        public int SessionStreamDispatcherType { get; set; }
    }
}