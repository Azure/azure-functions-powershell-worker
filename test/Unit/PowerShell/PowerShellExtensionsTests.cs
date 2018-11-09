//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//
using System.Collections;
using System.Collections.ObjectModel;

using Microsoft.Azure.Functions.PowerShellWorker.PowerShell;
using Xunit;

namespace Microsoft.Azure.Functions.PowerShellWorker.Test
{
    using System.Management.Automation;

    public class PowerShellExtensionsTests
    {
        [Fact]
        public void CanInvokeAndClearCommands()
        {
            var pwsh = PowerShell.Create().AddCommand("Get-Process");
            Assert.Single(pwsh.Commands.Commands);

            pwsh.InvokeAndClearCommands();
            Assert.Empty(pwsh.Commands.Commands);
        }

        [Fact]
        public void CanInvokeAndClearCommandsWithReturnValue()
        {
            var data = 5;
            var pwsh = PowerShell.Create().AddScript($"function Get-Value() {{ {data} }}; Get-Value");
            Assert.Single(pwsh.Commands.Commands);

            Collection<int> results = pwsh.InvokeAndClearCommands<int>();
            Assert.Empty(pwsh.Commands.Commands);

            Assert.Single(results, data);
        }

        [Fact]
        public void CanFormatObjectsToString()
        {
            string expectedResult = @"
Name                           Value
----                           -----
Foo                            bar


";

            Hashtable data = new Hashtable {{ "Foo", "bar"}};   
            Assert.Equal(expectedResult, 
                PowerShell.Create().FormatObjectToString(data));
        }
    }
}
