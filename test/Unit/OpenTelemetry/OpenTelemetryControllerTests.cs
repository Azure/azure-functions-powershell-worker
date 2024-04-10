using Moq;
using System;
using Xunit;

using Microsoft.Azure.Functions.PowerShellWorker.OpenTelemetry;

namespace Microsoft.Azure.Functions.PowerShellWorker.Test.OpenTelemetry
{
    public class OpenTelemetryControllerTests
    {
        private readonly Mock<IOpenTelemetryServices> _mockOtelServices = new Mock<IOpenTelemetryServices>(MockBehavior.Strict);

        [Theory]
        [InlineData("true", true)]
        [InlineData("false", false)]
        [InlineData(null, false)]
        internal void OpenTelemetryEnvironmentVariableCheckWorking(string? environmentVariableValue, bool desiredResult)
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

        private OpenTelemetryController CreateOpenTelemetryController()
        {
            return new OpenTelemetryController(_mockOtelServices.Object);
        }
    }
}
