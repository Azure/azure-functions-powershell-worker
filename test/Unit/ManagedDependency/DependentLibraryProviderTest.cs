//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//
namespace Microsoft.Azure.Functions.PowerShellWorker.Test.Unit.ManagedDependency
{
    using System;
    using System.Collections;
    using System.Collections.ObjectModel;
    using System.IO;
    using System.Reflection;
    using System.Threading.Tasks;
    using Microsoft.Azure.Functions.PowerShellWorker.ManagedDependency;
    using Microsoft.Azure.Functions.PowerShellWorker.PowerShell;
    using Xunit;
    using System.Linq;

    public class DependentLibraryProviderTest
    {
        DependentLibraryProvider dependentLibraryProvider;
        public DependentLibraryProviderTest()
        {
            dependentLibraryProvider = new DependentLibraryProvider();
        }

        [Fact]
        public async Task TestGetDependentLibrarie()
        {
            try
            {             
                string hostFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"Resources\host.json");
                var result = await dependentLibraryProvider.GetDependentLibrariesAsync(hostFilePath);               
                Assert.True(result != null && result.ManagedDependencies != null && result.ManagedDependencies.Any());
            }
            catch (Exception)
            {
                Assert.Equal(1, 0);
            }
        }
    }
}
