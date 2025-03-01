using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.Agents.MCP.Core.Logging
{
    public static class LogEvents
    {
        public enum ErrorCode
        {
            // Standard JSON-RPC 2.0 errors
            ParseError = -32700,
            InvalidRequest = -32600,
            MethodNotFound = -32601,
            InvalidParams = -32602,
            InternalError = -32603,

            // Server error range
            ServerErrorStart = -32099,
            ServerErrorEnd = -32000,

            // Common extended error codes
            ResourceNotFound = -32001,
            ResourceUnavailable = -32002,
            TransactionRejected = -32003,
            MethodNotSupported = -32004,
            LimitExceeded = -32005,
            JsonRpcVersionNotSupported = -32006,

            // Application specific error codes
            Unauthorized = 1,
            ActionNotAllowed = 2,
            ExecutionError = 3,
            InvalidFormat = 7
        }
    }
}
