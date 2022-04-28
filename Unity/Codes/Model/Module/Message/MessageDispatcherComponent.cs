using System.Collections.Generic;

namespace ET
{
    /// <summary>
    /// 消息分发组件
    /// 虽然协议号与协议数据类型是1对1的关系，但是协议号与协议处理的类可以1对多
    /// 即一个协议数据，可以由多个协议处理类进行处理
    /// </summary>
    public class MessageDispatcherComponent : Entity, IAwake, IDestroy, ILoad
    {
        public static MessageDispatcherComponent Instance
        {
            get;
            set;
        }

        /// <summary>
        /// 协议ID对应 协议处理实例列表
        /// </summary>
        public readonly Dictionary<ushort, List<IMHandler>> Handlers = new Dictionary<ushort, List<IMHandler>>();
    }
}