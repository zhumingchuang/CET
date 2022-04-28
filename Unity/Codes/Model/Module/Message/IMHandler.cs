using System;

namespace ET
{
    /// <summary>
    /// ��Ϣ����ӿ�
    /// </summary>
    public interface IMHandler
    {
        /// <summary>
        /// ��Ϣ����
        /// </summary>
        /// <param name="session"></param>
        /// <param name="message">������Ϣ��������</param>
        void Handle(Session session, object message);

        /// <summary>
        /// ��ȡ��Ϣ������Ϣ
        /// </summary>
        /// <returns></returns>
        Type GetMessageType();

        Type GetResponseType();
    }
}