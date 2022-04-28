using System;

namespace ET
{
    /// <summary>
    /// 抽象消息处理 带返回数据
    /// </summary>
    /// <typeparam name="Request">请求</typeparam>
    /// <typeparam name="Response">响应</typeparam>
    [MessageHandler]
    public abstract class AMRpcHandler<Request, Response>: IMHandler where Request : class, IRequest where Response : class, IResponse
    {
        protected abstract ETTask Run(Session session, Request request, Response response, Action reply);

        /// <summary>
        /// 消息处理
        /// </summary>
        /// <param name="session"></param>
        /// <param name="message">处理消息的数据类</param>
        public void Handle(Session session, object message)
        {
            HandleAsync(session, message).Coroutine();
        }

        private async ETTask HandleAsync(Session session, object message)
        {
            try
            {
                //转换为对应请求类
                Request request = message as Request;
                if (request == null)
                {
                    throw new Exception($"消息类型转换错误: {message.GetType().Name} to {typeof (Request).Name}");
                }

                //RpcId 用来标识一个传入协议数据
                int rpcId = request.RpcId;

                long instanceId = session.InstanceId;

                Response response = Activator.CreateInstance<Response>();

                void Reply()
                {
                    // 等回调回来,session可以已经断开了,所以需要判断session InstanceId是否一样
                    if (session.InstanceId != instanceId)
                    {
                        return;
                    }

                    response.RpcId = rpcId;
                    session.Reply(response);
                }

                try
                {
                    await this.Run(session, request, response, Reply);
                }
                catch (Exception exception)
                {
                    Log.Error(exception);
                    response.Error = ErrorCore.ERR_RpcFail;
                    response.Message = exception.ToString();
                    Reply();
                }
            }
            catch (Exception e)
            {
                throw new Exception($"解释消息失败: {message.GetType().FullName}", e);
            }
        }

        /// <summary>
        /// 获取消息类型信息
        /// </summary>
        /// <returns></returns>
        public Type GetMessageType()
        {
            return typeof (Request);
        }

        /// <summary>
        /// 获取响应消息类型信息
        /// </summary>
        /// <returns></returns>
        public Type GetResponseType()
        {
            return typeof (Response);
        }
    }
}