//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

namespace Microsoft.Azure.Functions.PowerShellWorker.Test
{
    using System;
    using System.Linq;
    using System.Management.Automation;
    using System.Text.RegularExpressions;

    using Xunit;

    using Microsoft.Azure.Functions.PowerShellWorker.PowerShell;

    public class ErrorRecordFormatterTests
    {
        private static readonly ErrorRecordFormatter s_errorRecordFormatter = new ErrorRecordFormatter();

        [Fact]
        public void FormattedStringContainsBasicErrorRecordData()
        {
            var exception = new RuntimeException("My exception");
            var errorRecord = new ErrorRecord(exception, "error id", ErrorCategory.InvalidOperation, null);

            var result = s_errorRecordFormatter.Format(errorRecord);

            Assert.StartsWith(exception.Message, result);
            var resultLines = result.Split(Environment.NewLine);
            Assert.Contains(resultLines, line => Regex.IsMatch(line, @"\bCategoryInfo\b[:\s]*?\bInvalidOperation\b"));
            Assert.Contains(resultLines, line => Regex.IsMatch(line, @"\bFullyQualifiedErrorId\b[:\s]*?\berror id\b"));
            Assert.Contains(resultLines, line => line == "System.Management.Automation.RuntimeException: My exception");
        }

        [Fact]
        public void FormattedStringContainsInnerExceptions()
        {
            var exception1 = new Exception("My exception 1");
            var exception2 = new Exception("My exception 2", exception1);
            var exception3 = new Exception("My exception 3", exception2);
            var errorRecord = new ErrorRecord(exception3, "error id", ErrorCategory.InvalidOperation, null);

            var result = s_errorRecordFormatter.Format(errorRecord);

            var resultLines = result.Split(Environment.NewLine);
            Assert.Contains(resultLines, line => line == "Inner exception: System.Exception: My exception 1");
            Assert.Contains(resultLines, line => line == "Inner exception: System.Exception: My exception 2");
            Assert.Equal(2, resultLines.Count(line => line.Contains("Inner exception:")));
        }

        [Fact]
        public void FormattedStringContainsAggregateExceptionMembers()
        {
            var exception1 = new Exception("My exception 1");
            var exception2 = new Exception("My exception 2");
            var exception3 = new AggregateException("My exception 3", exception1, exception2);
            var exception4 = new Exception("My exception 4", exception3);
            var exception5 = new Exception("My exception 5");
            var exception6 = new AggregateException("My exception 6", exception4, exception5);
            var exception7 = new Exception("My exception 7", exception6);
            var errorRecord = new ErrorRecord(exception7, "error id", ErrorCategory.InvalidOperation, null);

            var result = s_errorRecordFormatter.Format(errorRecord);

            var resultLines = result.Split(Environment.NewLine);
            Assert.Contains(resultLines, line => line == "Inner exception: System.Exception: My exception 1");
            Assert.Contains(resultLines, line => line == "Inner exception: System.Exception: My exception 2");
            Assert.Contains(resultLines, line => line.StartsWith("Inner exception: System.AggregateException: My exception 3"));
            Assert.Contains(resultLines, line => line == "Inner exception: System.Exception: My exception 4");
            Assert.Contains(resultLines, line => line == "Inner exception: System.Exception: My exception 5");
            Assert.Contains(resultLines, line => line.StartsWith("Inner exception: System.AggregateException: My exception 6"));
            Assert.Contains(resultLines, line => line == "System.Exception: My exception 7");
        }

        [Fact]
        public void FormattedStringIsTruncatedIfTooLong()
        {
            var exception1 = new Exception("My exception 1");
            var exception2 = new Exception("My exception 2", exception1);
            var exception3 = new Exception("My exception 3", exception2);
            var errorRecord = new ErrorRecord(exception3, "error id", ErrorCategory.InvalidOperation, null);
            var fullResult = s_errorRecordFormatter.Format(errorRecord);

            var maxSize = fullResult.Length / 2;
            var truncatedResult = new ErrorRecordFormatter().Format(errorRecord, maxSize);

            const string ExpectedPostfix = "...";
            Assert.InRange(truncatedResult.Length, ExpectedPostfix.Length + 1, maxSize);
            Assert.EndsWith(ExpectedPostfix, truncatedResult);
            Assert.StartsWith(fullResult.Substring(0, truncatedResult.Length - ExpectedPostfix.Length), truncatedResult);
        }
    }
}
