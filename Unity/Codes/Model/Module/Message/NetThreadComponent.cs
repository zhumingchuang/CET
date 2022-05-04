using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;

namespace ET
{
    /// <summary>
    /// 网络服务管理 刷新服务
    /// </summary>
    public class NetThreadComponent: Entity, IAwake, ILateUpdate, IDestroy
    {
        public static NetThreadComponent Instance;
        
        public const int checkInteral = 2000;
        public const int recvMaxIdleTime = 60000;
        public const int sendMaxIdleTime = 60000;

        public Action<AService> foreachAction;

        public ThreadSynchronizationContext ThreadSynchronizationContext;
        
        public HashSet<AService> Services = new HashSet<AService>();
    }
}