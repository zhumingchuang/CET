using System;

namespace ET
{
    /// <summary>
    /// 消息处理接口
    /// </summary>
    public interface IMHandler
    {
        /// <summary>
        /// 消息处理
        /// </summary>
        /// <param name="session"></param>
        /// <param name="message">处理消息的数据类</param>
        void Handle(Session session, object message);

        /// <summary>
        /// 获取消息类型信息
        /// </summary>
        /// <returns></returns>
        Type GetMessageType();

        Type GetResponseType();
    }
}