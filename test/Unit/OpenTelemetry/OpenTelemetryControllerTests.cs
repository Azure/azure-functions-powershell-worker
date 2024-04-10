using Moq;
using System;
using Xunit;

using Microsoft.Azure.Functions.PowerShellWorker.OpenTelemetry;
using System.Linq;
using Microsoft.Azure.Functions.PowerShellWorker.Utility;

namespace Microsoft.Azure.Functions.PowerShellWorker.Test.OpenTelemetry
{
    using PowerShell = System.Management.Automation.PowerShell;

    public class OpenTelemetryControllerTests
    {
        private readonly Mock<ILogger> _mockLogger = new Mock<ILogger>(MockBehavior.Strict);
        private readonly Mock<IOpenTelemetryServices> _mockOtelServices;

        public OpenTelemetryControllerTests()
        {
            _mockOtelServices = new Mock<IOpenTelemetryServices>(MockBehavior.Strict);
        }

        [Theory]
        [InlineData("true", true)]
        [InlineData("false", false)]
        [InlineData(null, false)]
        internal void OpenTelemetryEnvironmentVariableCheckWorks(string? environmentVariableValue, bool desiredResult)
        {
            try
            {
                Environment.SetEnvironmentVariable("OTEL_FUNCTIONS_WORKER_ENABLED", environmentVariableValue);
                
                Assert.Equal(desiredResult, OpenTelemetryController.IsOpenTelemetryEnvironmentEnabled());
            }
            finally
            {
                Environment.SetEnvironmentVariable("OTEL_FUNCTIONS_WORKER_ENABLED", null);
                OpenTelemetryController.ResetOpenTelemetryModuleStatus();
            }
        }

        [Fact]
        internal void AddStartOpenTelemetryInvocationCommand_AddsCommands()
        {
            try
            {
                PowerShell _pwsh = PowerShell.Create();
                var _realOTelServices = new OpenTelemetryServices(_mockLogger.Object, _pwsh);

                OpenTelemetryController controller = CreateMockOpenTelemetryController();

                OpenTelemetryInvocationContext context = new OpenTelemetryInvocationContext(
                    "93d73ba2-bac9-41f9-ad31-e7ab56a6d7e1",
                    "00-59081e54d24b74f20957499295f4e835-b492942fa64debb3-00",
                    ""
                );

                Environment.SetEnvironmentVariable("OTEL_FUNCTIONS_WORKER_ENABLED", "true");
                _mockOtelServices.Setup(_ => _.IsModuleLoaded()).Returns(true);
                _mockOtelServices.Setup(_ => _.AddStartOpenTelemetryInvocationCommand(context))
                    .Callback(() => _realOTelServices.AddStartOpenTelemetryInvocationCommand(context));

                controller.AddStartOpenTelemetryInvocationCommand(context);

                Assert.Single(_pwsh.Commands.Commands);
                Assert.Equal("Start-OpenTelemetryInvocationInternal", _pwsh.Commands.Commands.First().CommandText);

                var parameters = _pwsh.Commands.Commands.First().Parameters;
                Assert.Equal(3, parameters.Count);
                Assert.Equal("InvocationId", parameters.ElementAt(0).Name);
                Assert.Equal(context.InvocationId, parameters.ElementAt(0).Value);
                Assert.Equal("TraceParent", parameters.ElementAt(1).Name);
                Assert.Equal(context.TraceParent, parameters.ElementAt(1).Value);
                Assert.Equal("TraceState", parameters.ElementAt(2).Name);
                Assert.Equal(context.TraceState, parameters.ElementAt(2).Value);

                _mockOtelServices.Verify(_ => _.AddStartOpenTelemetryInvocationCommand(context), Times.Once);
            }
            finally
            {
                Environment.SetEnvironmentVariable("OTEL_FUNCTIONS_WORKER_ENABLED", null);
                OpenTelemetryController.ResetOpenTelemetryModuleStatus();
            }
        }

        [Fact]
        internal void StopOpenTelemetryInvocationCommand_AddsCommands()
        {
            try
            {
                PowerShell _pwsh = PowerShell.Create();
                var _realOTelServices = new OpenTelemetryServices(_mockLogger.Object, _pwsh);

                OpenTelemetryController controller = CreateMockOpenTelemetryController();

                OpenTelemetryInvocationContext context = new OpenTelemetryInvocationContext(
                    "93d73ba2-bac9-41f9-ad31-e7ab56a6d7e1",
                    "00-59081e54d24b74f20957499295f4e835-b492942fa64debb3-00",
                    ""
                );

                Environment.SetEnvironmentVariable("OTEL_FUNCTIONS_WORKER_ENABLED", "true");
                _mockOtelServices.Setup(_ => _.IsModuleLoaded()).Returns(true);
                _mockOtelServices.Setup(_ => _.StopOpenTelemetryInvocation(context, true))
                    .Callback(() => _realOTelServices.StopOpenTelemetryInvocation(context, true));

                controller.StopOpenTelemetryInvocation(context, true);

                Assert.Single(_pwsh.Commands.Commands);
                Assert.Equal("Stop-OpenTelemetryInvocationInternal", _pwsh.Commands.Commands.First().CommandText);

                var parameters = _pwsh.Commands.Commands.First().Parameters;
                Assert.Single(parameters);
                Assert.Equal("InvocationId", parameters.ElementAt(0).Name);
                Assert.Equal(context.InvocationId, parameters.ElementAt(0).Value);

                _mockOtelServices.Verify(_ => _.StopOpenTelemetryInvocation(context, true), Times.Once);
            }
            finally
            {
                Environment.SetEnvironmentVariable("OTEL_FUNCTIONS_WORKER_ENABLED", null);
                OpenTelemetryController.ResetOpenTelemetryModuleStatus();
            }
        }

        [Fact]
        internal void StartFunctionsLoggingListener_CorrectCommands()
        {
            try
            {
                PowerShell _pwsh = PowerShell.Create();
                var _realOTelServices = new OpenTelemetryServices(_mockLogger.Object, _pwsh);

                OpenTelemetryController controller = CreateMockOpenTelemetryController();

                Environment.SetEnvironmentVariable("OTEL_FUNCTIONS_WORKER_ENABLED", "true");
                _mockOtelServices.Setup(_ => _.IsModuleLoaded()).Returns(true);
                _mockOtelServices.Setup(_ => _.StartFunctionsLoggingListener(true))
                    .Callback(() => _realOTelServices.StartFunctionsLoggingListener(true));

                controller.StartFunctionsLoggingListener(true);

                Assert.Single(_pwsh.Commands.Commands);
                Assert.Equal("Get-FunctionsLogHandlerInternal", _pwsh.Commands.Commands.First().CommandText);

                var parameters = _pwsh.Commands.Commands.First().Parameters;
                Assert.Empty(parameters);

                _mockOtelServices.Verify(_ => _.StartFunctionsLoggingListener(true), Times.Once);
            }
            finally
            {
                Environment.SetEnvironmentVariable("OTEL_FUNCTIONS_WORKER_ENABLED", null);
                OpenTelemetryController.ResetOpenTelemetryModuleStatus();
            }
        }

        private OpenTelemetryController CreateMockOpenTelemetryController()
        {
            return new OpenTelemetryController(_mockOtelServices.Object);
        }
    }
}
