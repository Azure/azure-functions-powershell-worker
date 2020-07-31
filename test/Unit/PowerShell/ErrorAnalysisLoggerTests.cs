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
        private const string FakeUnknownCommand = "Unknown-Command";

        private readonly ErrorRecord _fakeErrorRecord =
            new ErrorRecord(
                new CommandNotFoundException("Exception message") { CommandName = FakeUnknownCommand },
                "CommandNotFoundException",
                ErrorCategory.ObjectNotFound,
                FakeUnknownCommand);

        private Mock<ILogger> _mockLogger = new Mock<ILogger>();

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

            _mockLogger.VerifyNoOtherCalls();
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void LogsCommandNotFoundToNonUserOnlyLog(bool isException)
        {
            ErrorAnalysisLogger.Log(_mockLogger.Object, _fakeErrorRecord, isException);

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
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void LogsCommandNotFoundToUserOnlyLog(bool isException)
        {
            ErrorAnalysisLogger.Log(_mockLogger.Object, _fakeErrorRecord, isException);

            _mockLogger.Verify(
                _ => _.Log(
                    true,
                    LogLevel.Warning,
                    It.Is<string>(message => message.Contains(FakeUnknownCommand)),
                    null),
                Times.Once);
        }
    }
}
