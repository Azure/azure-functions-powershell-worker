//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

namespace Microsoft.Azure.Functions.PowerShellWorker.Test
{
    using System;
    using System.Management.Automation;

    using Microsoft.Azure.Functions.PowerShellWorker.PowerShell;
    using Microsoft.Azure.Functions.PowerShellWorker.Utility;
    using LogLevel = Microsoft.Azure.WebJobs.Script.Grpc.Messages.RpcLog.Types.Level;

    using Moq;
    using Xunit;

    public class ErrorAnalysisLoggerTests
    {
        private readonly Mock<ILogger> _mockLogger = new Mock<ILogger>();

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void DoesNotLogUnknownErrors(bool isException)
        {
            var error = new ErrorRecord(
                                new Exception(),
                                "UnknownException",
                                ErrorCategory.NotSpecified,
                                "Dummy target object");

            ErrorAnalysisLogger.Log(_mockLogger.Object, error, isException);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void LogsCommandNotFound(bool isException)
        {
            const string FakeUnknownCommand = "Unknown-Command";

            var error = CreateCommandNotFoundError(FakeUnknownCommand);

            ErrorAnalysisLogger.Log(_mockLogger.Object, error, isException);

            _mockLogger.Verify(
                _ => _.Log(
                    false,
                    LogLevel.Warning,
                    It.Is<string>(
                        message => message.Contains("CommandNotFoundException")
                                    && (isException && message.Contains("(exception)") && !message.Contains("(error)")
                                        || !isException && message.Contains("(error)") && !message.Contains("(exception)"))
                                    && !message.Contains(FakeUnknownCommand)),
                    null),
                Times.Once);

            _mockLogger.Verify(
                _ => _.Log(
                    true,
                    LogLevel.Warning,
                    It.Is<string>(message => message.Contains(FakeUnknownCommand)),
                    null),
                Times.Once);

            _mockLogger.VerifyNoOtherCalls();
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void LogsModuleNotFound(bool isException)
        {
            const string FakeUnknownModule = "UnknownModule";

            var error = CreateModuleNotFoundError(FakeUnknownModule);

            ErrorAnalysisLogger.Log(_mockLogger.Object, error, isException);

            _mockLogger.Verify(
                _ => _.Log(
                    false,
                    LogLevel.Warning,
                    It.Is<string>(
                        message => message.Contains("ModuleNotFound")
                                    && (isException && message.Contains("(exception)") && !message.Contains("(error)")
                                        || !isException && message.Contains("(error)") && !message.Contains("(exception)"))
                                    && !message.Contains(FakeUnknownModule)),
                    null),
                Times.Once);

            _mockLogger.Verify(
                _ => _.Log(
                    true,
                    LogLevel.Warning,
                    It.Is<string>(message => message.Contains(FakeUnknownModule)),
                    null),
                Times.Once);

            _mockLogger.VerifyNoOtherCalls();
        }

        private static ErrorRecord CreateCommandNotFoundError(string commandName)
        {
            using var ps = PowerShell.Create();
            ps.AddCommand(commandName);
            try
            {
                ps.Invoke();
            }
            catch (CommandNotFoundException e)
            {
                return e.ErrorRecord;
            }

            throw new Exception("Expected CommandNotFoundException is not thrown. Incompatible PowerShell version?");
        }

        private static ErrorRecord CreateModuleNotFoundError(string moduleName)
        {
            return new ErrorRecord(
                        new Exception(),
                        "Modules_ModuleNotFound,Microsoft.PowerShell.Commands.ImportModuleCommand",
                        ErrorCategory.ResourceUnavailable,
                        moduleName);
        }
    }
}
