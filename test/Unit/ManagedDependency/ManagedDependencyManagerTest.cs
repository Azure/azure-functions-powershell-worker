//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

namespace Microsoft.Azure.Functions.PowerShellWorker.Test.Unit.ManagedDependency
{
    using Microsoft.Azure.Functions.PowerShellWorker.ManagedDependency;
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Reflection;
    using System.Text;
    using Xunit;
    public class ManagedDependencyManagerTest
    {
        [Fact]
        public void TestAddAzModulesPath()
        {            
            var hostJsonRootPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources");
            var functionModulePath = Path.Combine(hostJsonRootPath, "Modules");
            var result = ManagedDependencyManager.AddAzModulesPath(hostJsonRootPath, functionModulePath);
            Assert.Equal(2, result.Split(';').Length);
        }
    }
}

