using System.Net;

namespace ET
{
    /// <summary>
    /// Kcp网络协议组件
    /// </summary>
    [ChildType(typeof(Session))]
    public class NetKcpComponent: Entity, IAwake<int>, IAwake<IPEndPoint, int>, IDestroy
    {
        public AService Service;
        
        public int SessionStreamDispatcherType { get; set; }
    }
}