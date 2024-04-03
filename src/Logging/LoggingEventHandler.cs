using System;
using System.Collections.Generic;

namespace Microsoft.Azure.Functions.PowerShellWorker.Utility
{
    internal class LoggingEventHandler
    {
        private HashSet<Action<string, string, Exception>> _eventHandlers = new HashSet<Action<string, string, Exception>>();

        public void Subscribe(Action<string, string, Exception> handler)
        {
            _eventHandlers.Add(handler);
        }

        public void Unsubscribe(Action<string, string, Exception> handler)
        {
            _eventHandlers.Remove(handler);
        }

        public void LogToHandlers(string level, string message, Exception exception = null)
        {
            foreach (var handler in _eventHandlers)
            {
                handler(level, message, exception);
            }
        }
    }
}
