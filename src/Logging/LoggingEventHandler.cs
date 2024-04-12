using System;
using System.Collections.Generic;

namespace Microsoft.Azure.Functions.PowerShellWorker.Utility
{
    internal class LoggingEventHandler
    {
        private Action<string, string, Exception> _eventHandler = (a, b, c) => { }; 

        public void Subscribe(Action<string, string, Exception> handler)
        {
            _eventHandler = handler;
        }

        public void LogToHandlers(string level, string message, Exception exception = null)
        {
            _eventHandler(level, message, exception);
        }
    }
}
