﻿using System.Collections.Generic;

namespace ET
{
    public class ActorMessageSenderComponent: Entity, IAwake, IDestroy
    {
        /// <summary>
        /// 超时时间
        /// </summary>
        public static long TIMEOUT_TIME = 40 * 1000;

        public static ActorMessageSenderComponent Instance { get; set; }

        public int RpcId;

        public readonly SortedDictionary<int, ActorMessageSender> requestCallback = new SortedDictionary<int, ActorMessageSender>();

        public long TimeoutCheckTimer;

        public List<int> TimeoutActorMessageSenders = new List<int>();
    }
}