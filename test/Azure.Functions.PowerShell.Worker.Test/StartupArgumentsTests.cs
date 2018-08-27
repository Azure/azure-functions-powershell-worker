using System;
using Microsoft.Azure.Functions.PowerShellWorker;
using Xunit;

namespace Azure.Functions.PowerShell.Worker.Test
{
    public class StartupArgumentsTests
    {
        [Fact]
        public void TestStartupArumentsParse()
        {
            var host = "0.0.0.0";
            var port = 1234;
            var workerId = Guid.NewGuid().ToString();
            var requestId = Guid.NewGuid().ToString();
            var grpcMaxMessageLength = 100;
            var args = $"--host {host} --port {port} --workerId {workerId} --requestId {requestId} --grpcMaxMessageLength {grpcMaxMessageLength}";

            var startupArguments = StartupArguments.Parse(args.Split(' '));

            Assert.Equal(host, startupArguments.Host);
            Assert.Equal(port, startupArguments.Port);
            Assert.Equal(workerId, startupArguments.WorkerId);
            Assert.Equal(requestId, startupArguments.RequestId);
            Assert.Equal(grpcMaxMessageLength, startupArguments.GrpcMaxMessageLength);
        }

        [Fact]
        public void TestStartupArumentsParseThrows()
        {
            var host = "0.0.0.0";
            var port = 1234;
            var workerId = Guid.NewGuid().ToString();
            var requestId = Guid.NewGuid().ToString();
            var args = $"--host {host} --port {port} --workerId {workerId} --requestId {requestId} --grpcMaxMessageLength";

            Assert.Throws<InvalidOperationException>(() => StartupArguments.Parse(args.Split(' ')));
        }
    }
}
