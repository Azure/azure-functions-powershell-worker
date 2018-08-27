//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System;
using System.Globalization;
using System.Management.Automation.Host;

using Microsoft.Azure.Functions.PowerShellWorker.Utility;

namespace Microsoft.Azure.Functions.PowerShellWorker.PowerShell.Host
{
    /// <summary>
    /// A sample implementation of the PSHost abstract class for console
    /// applications. Not all members are implemented. Those that aren't throw a
    /// NotImplementedException.
    /// </summary>
    class AzureFunctionsPowerShellHost : PSHost
    {
        /// <summary>
        /// The private reference of the logger.
        /// </summary>
        RpcLogger _logger { get; set; }

        /// <summary>
        /// Creates an instance of the PSHostUserInterface object for this
        /// application.
        /// </summary>
        HostUserInterface HostUI { get; set; }

        /// <summary>
        /// The culture info of the thread that created
        /// this object.
        /// </summary>
        readonly CultureInfo originalCultureInfo = System.Threading.Thread.CurrentThread.CurrentCulture;

        /// <summary>
        /// The UI culture info of the thread that created
        /// this object.
        /// </summary>
        readonly CultureInfo originalUICultureInfo = System.Threading.Thread.CurrentThread.CurrentUICulture;

        /// <summary>
        /// The identifier of the PSHost implementation.
        /// </summary>
        Guid Id = Guid.NewGuid();

        /// <summary>
        /// Gets the culture info to use - this implementation just snapshots the
        /// curture info of the thread that created this object.
        /// </summary>
        public override CultureInfo CurrentCulture => originalCultureInfo;

        /// <summary>
        /// Gets the UI culture info to use - this implementation just snapshots the
        /// UI curture info of the thread that created this object.
        /// </summary>
        public override CultureInfo CurrentUICulture => originalUICultureInfo;

        /// <summary>
        /// Gets an identifier for this host. This implementation always returns 
        /// the GUID allocated at instantiation time.
        /// </summary>
        public override Guid InstanceId => Id;

        /// <summary>
        /// Gets an appropriate string to identify you host implementation.
        /// Keep in mind that this string may be used by script writers to identify
        /// when your host is being used.
        /// </summary>
        public override string Name => "AzureFunctionsHost";

        /// <summary>
        /// Gets the implementation of the PSHostUserInterface class. 
        /// </summary>
        public override PSHostUserInterface UI => HostUI;

        /// <summary>
        /// Return the version object for this application. Typically this should match the version
        /// resource in the application.
        /// </summary>
        public override Version Version => new Version(1, 0, 0, 0);

        public AzureFunctionsPowerShellHost(RpcLogger logger)
        {
            _logger = logger;
            HostUI = new HostUserInterface(logger);
        }

        /// <summary>
        /// Not implemented by this class. The call fails with an exception.
        /// </summary>
        public override void EnterNestedPrompt() =>
            throw new NotImplementedException("The method or operation is not implemented.");

        /// <summary>
        /// Not implemented by this class. The call fails with an exception.
        /// </summary>
        public override void ExitNestedPrompt() =>
            throw new NotImplementedException("The method or operation is not implemented.");

        /// <summary>
        /// This API is called before an external application process is started. Typically
        /// it's used to save state that the child process may alter so the parent can
        /// restore that state when the child exits. In this, we don't need this so
        /// the method simple returns.
        /// </summary>
        public override void NotifyBeginApplication() { return; } // Do nothing.

        /// <summary>
        /// This API is called after an external application process finishes. Typically
        /// it's used to restore state that the child process may have altered. In this,
        /// we don't need this so the method simple returns.
        /// </summary>
        public override void NotifyEndApplication() { return; } // Do nothing.

        /// <summary>
        /// Indicate to the host application that exit has
        /// been requested. Pass the exit code that the host
        /// application should use when exiting the process.
        /// </summary>
        /// <param name="exitCode">The exit code that the host application should use.</param>
        public override void SetShouldExit(int exitCode) =>
            throw new NotImplementedException("The method or operation is not implemented.");
    }
}

