using System;
using System.Collections.Generic;

namespace ET
{
    [ObjectSystem]
    public class MessageDispatcherComponentAwakeSystem: AwakeSystem<MessageDispatcherComponent>
    {
        public override void Awake(MessageDispatcherComponent self)
        {
            MessageDispatcherComponent.Instance = self;
            self.Load();
        }
    }

    [ObjectSystem]
    public class MessageDispatcherComponentLoadSystem: LoadSystem<MessageDispatcherComponent>
    {
        public override void Load(MessageDispatcherComponent self)
        {
            self.Load();
        }
    }

    [ObjectSystem]
    public class MessageDispatcherComponentDestroySystem: DestroySystem<MessageDispatcherComponent>
    {
        public override void Destroy(MessageDispatcherComponent self)
        {
            MessageDispatcherComponent.Instance = null;
            self.Handlers.Clear();
        }
    }

    /// <summary>
    /// 消息分发组件
    /// </summary>
    [FriendClass(typeof(MessageDispatcherComponent))]
    public static class MessageDispatcherComponentHelper
    {
        public static void Load(this MessageDispatcherComponent self)
        {
            self.Handlers.Clear();

            List<Type> types = Game.EventSystem.GetTypes(typeof (MessageHandlerAttribute));

            foreach (Type type in types)
            {
                IMHandler iMHandler = Activator.CreateInstance(type) as IMHandler;
                if (iMHandler == null)
                {
                    Log.Error($"message handle {type.Name} 需要继承 IMHandler");
                    continue;
                }

                Type messageType = iMHandler.GetMessageType();
                ushort opcode = OpcodeTypeComponent.Instance.GetOpcode(messageType);
                if (opcode == 0)
                {
                    Log.Error($"消息opcode为0: {messageType.Name}");
                    continue;
                }

                self.RegisterHandler(opcode, iMHandler);
            }
        }

        /// <summary>
        /// 根据协议ID 添加到消息处理列表
        /// </summary>
        /// <param name="self"></param>
        /// <param name="opcode">协议ID</param>
        /// <param name="handler">协议处理实例</param>
        public static void RegisterHandler(this MessageDispatcherComponent self, ushort opcode, IMHandler handler)
        {
            if (!self.Handlers.ContainsKey(opcode))
            {
                self.Handlers.Add(opcode, new List<IMHandler>());
            }

            self.Handlers[opcode].Add(handler);
        }

        /// <summary>
        /// 通过协议ID获取对应处理类的列表  调用各处理方法的实例
        /// </summary>
        /// <param name="self"></param>
        /// <param name="session"></param>
        /// <param name="opcode">协议ID</param>
        /// <param name="message"></param>
        public static void Handle(this MessageDispatcherComponent self, Session session, ushort opcode, object message)
        {
            List<IMHandler> actions;
            if (!self.Handlers.TryGetValue(opcode, out actions))
            {
                Log.Error($"消息没有处理: {opcode} {message}");
                return;
            }

            foreach (IMHandler ev in actions)
            {
                try
                {
                    ev.Handle(session, message);
                }
                catch (Exception e)
                {
                    Log.Error(e);
                }
            }
        }
    }
}