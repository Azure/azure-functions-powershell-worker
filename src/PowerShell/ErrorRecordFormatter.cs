//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

namespace Microsoft.Azure.Functions.PowerShellWorker.PowerShell
{
    using System;
    using System.Linq;
    using System.Management.Automation;
    using System.Text;

    internal class ErrorRecordFormatter
    {
        private const string TruncationPostfix = "...";
        private const string Indent = "   ";

        private readonly PowerShell _pwsh = PowerShell.Create();

        /// <summary>
        /// maxSize limits the maximum size of the formatted error string (in characters).
        /// The rest will be truncated. This value should be high enough to allow the result
        /// contain the most important and relevant information, but low enough to create
        /// no problems for the communication channels used to propagate this data.
        /// The default value is somewhat arbitrary but satisfies both conditions.
        /// </summary>
        public string Format(ErrorRecord errorRecord, int maxSize = 1 * 1024 * 1024)
        {
            _pwsh.AddScript("$ErrorView = 'NormalView'").InvokeAndClearCommands();
            var errorDetails = _pwsh.AddCommand("Microsoft.PowerShell.Utility\\Out-String")
                                    .AddParameter("InputObject", errorRecord)
                                    .InvokeAndClearCommands<string>();

            var result = new StringBuilder(
                                capacity: Math.Min(1024, maxSize),
                                maxCapacity: maxSize);

            try
            {
                result.Append(errorDetails.Single());
                result.AppendLine("Script stack trace:");
                AppendStackTrace(result, errorRecord.ScriptStackTrace, Indent);
                result.AppendLine();

                if (errorRecord.Exception != null)
                {
                    AppendExceptionWithInners(result, errorRecord.Exception);
                }

                return result.ToString();
            }
            catch (ArgumentOutOfRangeException) // exceeding StringBuilder max capacity
            {
                return Truncate(result, maxSize);
            }
        }

        private static void AppendExceptionWithInners(StringBuilder result, Exception exception)
        {
            AppendExceptionInfo(result, exception);

            if (exception is AggregateException aggregateException)
            {
                foreach (var innerException in aggregateException.Flatten().InnerExceptions)
                {
                    AppendInnerExceptionIfNotNull(result, innerException);
                }
            }
            else
            {
                AppendInnerExceptionIfNotNull(result, exception.InnerException);
            }
        }

        private static void AppendInnerExceptionIfNotNull(StringBuilder result, Exception innerException)
        {
            if (innerException != null)
            {
                result.Append("Inner exception: ");
                AppendExceptionWithInners(result, innerException);
            }
        }

        private static void AppendExceptionInfo(StringBuilder stringBuilder, Exception exception)
        {
            stringBuilder.Append(exception.GetType().FullName);
            stringBuilder.Append(": ");
            stringBuilder.AppendLine(exception.Message);

            AppendStackTrace(stringBuilder, exception.StackTrace, string.Empty);
            stringBuilder.AppendLine();
        }

        private static void AppendStackTrace(StringBuilder stringBuilder, string stackTrace, string indent)
        {
            if (stackTrace != null)
            {
                stringBuilder.Append(indent);
                stringBuilder.AppendLine(stackTrace.Replace(Environment.NewLine, Environment.NewLine + indent));
            }
        }

        private static string Truncate(StringBuilder result, int maxSize)
        {
            var charactersToRemove = result.Length + TruncationPostfix.Length - maxSize;
            if (charactersToRemove > 0)
            {
                result.Remove(result.Length - charactersToRemove, charactersToRemove);
            }

            return result + TruncationPostfix;
        }
    }
}
