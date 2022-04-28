using System;

namespace ET
{
    /// <summary>
    /// 抽象消息处理 没有返回消息
    /// </summary>
    /// <typeparam name="Message">处理对应消息类</typeparam>
    [MessageHandler]
    public abstract class AMHandler<Message>: IMHandler where Message : class
    {
        protected abstract void Run(Session session, Message message);

        /// <summary>
        /// 消息处理
        /// </summary>
        /// <param name="session"></param>
        /// <param name="msg">处理消息的数据类</param>
        public void Handle(Session session, object msg)
        {
            //将传进来的msg转换为模板类
            Message message = msg as Message;
            if (message == null)
            {
                Log.Error($"消息类型转换错误: {msg.GetType().Name} to {typeof (Message).Name}");
                return;
            }

            if (session.IsDisposed)
            {
                Log.Error($"session disconnect {msg}");
                return;
            }

            this.Run(session, message);
        }

        public Type GetMessageType()
        {
            return typeof (Message);
        }

        public Type GetResponseType()
        {
            return null;
        }
    }
}