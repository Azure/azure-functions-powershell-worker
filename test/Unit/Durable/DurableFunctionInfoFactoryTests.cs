﻿//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

namespace Microsoft.Azure.Functions.PowerShellWorker.Test.Durable
{
    using Google.Protobuf.Collections;
    using Microsoft.Azure.WebJobs.Script.Grpc.Messages;

    using Xunit;

    using Microsoft.Azure.Functions.PowerShellWorker.DurableWorker;

    public class DurableFunctionInfoFactoryTests
    {
        [Fact]
        public void ContainsDurableClientName()
        {
            var bindings = new MapField<string, BindingInfo>
            {
                {
                    "TestBindingName",
                    new BindingInfo { Direction = BindingInfo.Types.Direction.In, Type = "durableClient" }
                }
            };

            var durableFunctionInfo = DurableFunctionInfoFactory.Create(bindings);

            Assert.True(durableFunctionInfo.IsDurableClient);
            Assert.Equal("TestBindingName", durableFunctionInfo.DurableClientBindingName);
        }

        [Fact]
        public void ContainsFirstInputDurableClientName()
        {
            var bindings = new MapField<string, BindingInfo>
            {
                {
                    "Binding1",
                    new BindingInfo { Direction = BindingInfo.Types.Direction.In, Type = "anotherBindingType" }
                },
                {
                    "Binding2",
                    new BindingInfo { Direction = BindingInfo.Types.Direction.Out, Type = "durableClient" }
                },
                {
                    "Binding3",
                    new BindingInfo { Direction = BindingInfo.Types.Direction.Inout, Type = "durableClient" }
                },
                {
                    "Binding4",
                    new BindingInfo { Direction = BindingInfo.Types.Direction.In, Type = "durableClient" }
                },
                {
                    "Binding5",
                    new BindingInfo { Direction = BindingInfo.Types.Direction.In, Type = "yetAnotherBindingType" }
                }
            };

            var durableFunctionInfo = DurableFunctionInfoFactory.Create(bindings);

            Assert.True(durableFunctionInfo.IsDurableClient);
            Assert.Equal("Binding4", durableFunctionInfo.DurableClientBindingName);
        }

        [Fact]
        public void ContainsNoDurableClientNameIfNoBindings()
        {
            var durableFunctionInfo = DurableFunctionInfoFactory.Create(new MapField<string, BindingInfo>());

            Assert.False(durableFunctionInfo.IsDurableClient);
            Assert.Null(durableFunctionInfo.DurableClientBindingName);
        }

        [Fact]
        public void ContainsNoDurableClientNameIfNoInputDurableClientBindings()
        {
            var durableFunctionInfo = DurableFunctionInfoFactory.Create(new MapField<string, BindingInfo>());

            Assert.False(durableFunctionInfo.IsDurableClient);
            Assert.Null(durableFunctionInfo.DurableClientBindingName);
        }

        [Fact]
        public void ContainsNoDurableClientNameIfBindingNameIsEmpty()
        {
            var bindings = new MapField<string, BindingInfo>
            {
                {
                    string.Empty,
                    new BindingInfo { Direction = BindingInfo.Types.Direction.In, Type = "durableClient" }
                }
            };

            var durableFunctionInfo = DurableFunctionInfoFactory.Create(bindings);

            Assert.False(durableFunctionInfo.IsDurableClient);
            Assert.Null(durableFunctionInfo.DurableClientBindingName);
        }

        [Fact]
        public void TypeIsOrchestrationIfOrchestrationTriggerInputBindingFound()
        {
            var bindings = new MapField<string, BindingInfo>
            {
                {
                    "TestBindingName",
                    new BindingInfo { Direction = BindingInfo.Types.Direction.In, Type = "orchestrationTrigger" }
                }
            };

            var durableFunctionInfo = DurableFunctionInfoFactory.Create(bindings);

            Assert.Equal(DurableFunctionType.OrchestrationFunction, durableFunctionInfo.Type);
        }

        [Fact]
        public void TypeIsActivityIfActivityTriggerInputBindingFound()
        {
            var bindings = new MapField<string, BindingInfo>
            {
                {
                    "TestBindingName",
                    new BindingInfo { Direction = BindingInfo.Types.Direction.In, Type = "activityTrigger" }
                }
            };

            var durableFunctionInfo = DurableFunctionInfoFactory.Create(bindings);

            Assert.Equal(DurableFunctionType.ActivityFunction, durableFunctionInfo.Type);
        }

        [Fact]
        public void TypeIsNoneIfNoOrchestrationOrActivityTriggerFound()
        {
            var bindings = new MapField<string, BindingInfo>
            {
                {
                    "Binding1",
                    new BindingInfo { Direction = BindingInfo.Types.Direction.In, Type = "durableClient" }
                },
                {
                    // orchestrationTrigger, but not Direction.In
                    "Binging2",
                    new BindingInfo { Direction = BindingInfo.Types.Direction.Out, Type = "orchestrationTrigger" }
                },
                {
                    // activityTrigger, but not Direction.In
                    "Binging3",
                    new BindingInfo { Direction = BindingInfo.Types.Direction.Out, Type = "activityTrigger" }
                }
            };

            var durableFunctionInfo = DurableFunctionInfoFactory.Create(bindings);

            Assert.Equal(DurableFunctionType.None, durableFunctionInfo.Type);
        }
    }
}
