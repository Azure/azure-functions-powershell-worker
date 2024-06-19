using Moq;
using System;
using Xunit;

using Microsoft.Azure.Functions.PowerShellWorker.OpenTelemetry;
using System.Linq;
using Microsoft.Azure.Functions.PowerShellWorker.Utility;

namespace Microsoft.Azure.Functions.PowerShellWorker.Test.OpenTelemetry
{
    using PowerShell = System.Management.Automation.PowerShell;

    [Collection("Sequence")]
    public class OpenTelemetryControllerTests
    {
        // These constant values will work because they are not actually passed to the module
        // The module would fail with these inputs, it needs real invocation id and trace information
        private const string FakeInvocationID = "Fake InvocationID";
        private const string FakeTraceParent = "Fake TraceParent";

        private const string OTelEnabledEnvironmentVariableName = "OTEL_FUNCTIONS_WORKER_ENABLED";
        private readonly Mock<ILogger> _mockLogger = new Mock<ILogger>(MockBehavior.Strict);
        private readonly Mock<IPowerShellServicesForOpenTelemetry> _mockOtelServices;

        public OpenTelemetryControllerTests()
        {
            _mockOtelServices = new Mock<IPowerShellServicesForOpenTelemetry>(MockBehavior.Strict);
        }

        [Theory]
        [InlineData("True", true)]
        [InlineData("False", false)]
        [InlineData("true", true)]
        [InlineData("false", false)]
        [InlineData(null, false)]
        internal void OpenTelemetryEnvironmentVariableCheckWorks(string? environmentVariableValue, bool desiredResult)
        {
            try
            {
                Environment.SetEnvironmentVariable(OTelEnabledEnvironmentVariableName, environmentVariableValue);
                OpenTelemetryController.ResetOpenTelemetryModuleStatus();
                
                Assert.Equal(desiredResult, OpenTelemetryController.IsOpenTelemetryEnvironmentEnabled());
            }
            finally
            {
                Environment.SetEnvironmentVariable(OTelEnabledEnvironmentVariableName, null);
                OpenTelemetryController.ResetOpenTelemetryModuleStatus();
            }
        }

        [Fact]
        internal void AddStartOpenTelemetryInvocationCommand_AddsCommands()
        {
            try
            {
                PowerShell _pwsh = PowerShell.Create();
                var _realOTelServices = new PowerShellServicesForOpenTelemetry(_mockLogger.Object, _pwsh);

                OpenTelemetryController controller = CreateMockOpenTelemetryController();

                OpenTelemetryInvocationContext context = new OpenTelemetryInvocationContext(
                    FakeInvocationID,
                    FakeTraceParent,
                    string.Empty
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
                var _realOTelServices = new PowerShellServicesForOpenTelemetry(_mockLogger.Object, _pwsh);

                OpenTelemetryController controller = CreateMockOpenTelemetryController();

                OpenTelemetryInvocationContext context = new OpenTelemetryInvocationContext(
                    FakeInvocationID,
                    FakeTraceParent,
                    string.Empty
                );

                Environment.SetEnvironmentVariable("OTEL_FUNCTIONS_WORKER_ENABLED", "true");
                _mockOtelServices.Setup(_ => _.IsModuleLoaded()).Returns(true);
                _mockOtelServices.Setup(_ => _.StopOpenTelemetryInvocation(context, false))
                    .Callback(() => _realOTelServices.StopOpenTelemetryInvocation(context, false));

                controller.StopOpenTelemetryInvocation(context, false);

                Assert.Single(_pwsh.Commands.Commands);
                Assert.Equal("Stop-OpenTelemetryInvocationInternal", _pwsh.Commands.Commands.First().CommandText);

                var parameters = _pwsh.Commands.Commands.First().Parameters;
                Assert.Single(parameters);
                Assert.Equal("InvocationId", parameters.ElementAt(0).Name);
                Assert.Equal(context.InvocationId, parameters.ElementAt(0).Value);

                _mockOtelServices.Verify(_ => _.StopOpenTelemetryInvocation(context, false), Times.Once);
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
                var _realOTelServices = new PowerShellServicesForOpenTelemetry(_mockLogger.Object, _pwsh);

                OpenTelemetryController controller = CreateMockOpenTelemetryController();

                Environment.SetEnvironmentVariable("OTEL_FUNCTIONS_WORKER_ENABLED", "true");
                _mockOtelServices.Setup(_ => _.IsModuleLoaded()).Returns(true);
                _mockOtelServices.Setup(_ => _.StartFunctionsLoggingListener(false))
                    .Callback(() => _realOTelServices.StartFunctionsLoggingListener(false));

                controller.StartFunctionsLoggingListener(false);

                Assert.Single(_pwsh.Commands.Commands);
                Assert.Equal("Get-FunctionsLogHandlerInternal", _pwsh.Commands.Commands.First().CommandText);

                var parameters = _pwsh.Commands.Commands.First().Parameters;
                Assert.Empty(parameters);

                _mockOtelServices.Verify(_ => _.StartFunctionsLoggingListener(false), Times.Once);
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
