using System;
using Microsoft.Extensions.Logging;
using RpcResponseMessage = Phantasma.RpcClient.Client.RpcMessages.RpcResponseMessage;

namespace Phantasma.RpcClient.Client
{
    public class RpcLogger
    {
        public RpcLogger(ILogger log)
        {
            Log = log;
        }

        public ILogger Log { get; }
        public string RequestJsonMessage { get; private set; }
        public RpcResponseMessage ResponseMessage { get; private set; }

        public void LogRequest(string requestJsonMessage)
        {
            RequestJsonMessage = requestJsonMessage;
            if (IsLogTraceEnabled()) Log.LogTrace(GetRpcRequestLogMessage());
        }

        private string GetRpcRequestLogMessage()
        {
            return $"RPC Request: {RequestJsonMessage}";
        }

        private string GetRpcResponseLogMessage()
        {
            return ResponseMessage != null ? $"RPC Response: {ResponseMessage.Result}" : string.Empty;
        }

        private bool IsLogErrorEnabled()
        {
            return Log != null && Log.IsEnabled(LogLevel.Error);
        }

        public void LogResponse(RpcResponseMessage responseMessage)
        {
            ResponseMessage = responseMessage;

            if (IsLogTraceEnabled()) Log.LogTrace(GetRpcResponseLogMessage());

            if (HasError(responseMessage) && IsLogErrorEnabled())
            {
                if (!IsLogTraceEnabled()) Log.LogError(GetRpcResponseLogMessage());
                Log.LogError($"RPC Response Error: {responseMessage.Error.Message}");
            }
        }

        public void LogException(Exception ex)
        {
            if (IsLogErrorEnabled())
                Log.LogError("RPC Exception, " + GetRpcRequestLogMessage() + GetRpcResponseLogMessage(), ex);
        }

        private bool HasError(RpcResponseMessage message)
        {
            return message.Error != null && message.HasError;
        }

        private bool IsLogTraceEnabled()
        {
            return Log != null && Log.IsEnabled(LogLevel.Trace);
        }
    }
}