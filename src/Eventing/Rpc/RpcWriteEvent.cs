namespace Microsoft.Azure.Functions.PowerShellWorker
{
    internal class RpcWriteEvent : ScriptEvent
    {
        public RpcWriteEvent(string workerId, string invocationId): base(nameof(RpcChannelEvent), EventSources.Worker)
        {
            InvocationId = invocationId;
            WorkerId = workerId;
        }

        public string InvocationId { get; }
        public string WorkerId { get; }
    }
}
