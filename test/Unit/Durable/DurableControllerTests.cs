//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

namespace Microsoft.Azure.Functions.PowerShellWorker.Test.Durable
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;

    using Microsoft.Azure.WebJobs.Script.Grpc.Messages;
    using Microsoft.Azure.Functions.PowerShellWorker.DurableWorker;
    using Microsoft.Azure.Functions.PowerShellWorker.Durable;
    using Microsoft.Azure.Functions.PowerShellWorker.Utility;
    using Newtonsoft.Json;

    using Moq;
    using Xunit;

    public class DurableControllerTests
    {
        private readonly Mock<IPowerShellServices> _mockPowerShellServices = new Mock<IPowerShellServices>(MockBehavior.Strict);
        private readonly Mock<IOrchestrationInvoker> _mockOrchestrationInvoker = new Mock<IOrchestrationInvoker>(MockBehavior.Strict);

        // TODO: change the name of the method being tested to InitializeBindings
        [Fact]
        public void BeforeFunctionInvocation_SetsDurableClient_ForDurableClientFunction()
        {
            var durableController = CreateDurableController(DurableFunctionType.None, "DurableClientBindingName");

            var durableClient = new { FakeClientProperty = "FakeClientPropertyValue" };
            var inputData = new[]
            {
                CreateParameterBinding("AnotherParameter", "IgnoredValue"),
                CreateParameterBinding("DurableClientBindingName", durableClient),
                CreateParameterBinding("YetAnotherParameter", "IgnoredValue")
            };

            _mockPowerShellServices.Setup(_ => _.SetDurableClient(It.IsAny<object>()));

            durableController.InitializeBindings(inputData);

            _mockPowerShellServices.Verify(
                _ => _.SetDurableClient(
                    It.Is<object>(c => (string)((Hashtable)c)["FakeClientProperty"] == durableClient.FakeClientProperty)),
                Times.Once);
        }

        // TODO: change the name of the method being tested to InitializeBindings
        [Fact]
        public void BeforeFunctionInvocation_SetsOrchestrationContext_ForOrchestrationFunction()
        {
            var durableController = CreateDurableController(DurableFunctionType.OrchestrationFunction);
            const string contextParameterName = "ParameterName";
            var orchestrationContext = new OrchestrationContext { InstanceId = Guid.NewGuid().ToString() };
            var orchestrationBindingInfo = new OrchestrationBindingInfo(contextParameterName, orchestrationContext);
            var inputData = new[]
            {
                CreateParameterBinding("ParameterName", orchestrationContext)
            };

            Action<object> externalInvoker;
            _mockPowerShellServices.Setup(_ => _.SetOrchestrationContext(It.IsAny<ParameterBinding>(), out externalInvoker)).Returns(orchestrationBindingInfo);

            durableController.InitializeBindings(inputData);

            _mockPowerShellServices.Verify(
                _ => _.SetOrchestrationContext(
                    It.Is<ParameterBinding>(c => c.Data.ToString().Contains(orchestrationContext.InstanceId)), out externalInvoker),
                Times.Once);
        }

        // TODO: change the name of the method being tested to InitializeBindings
        [Fact]
        public void BeforeFunctionInvocation_Throws_OnOrchestrationFunctionWithoutContextParameter()
        {
            var durableController = CreateDurableController(DurableFunctionType.OrchestrationFunction);
            var inputData = new ParameterBinding[0];

            Assert.ThrowsAny<ArgumentException>(() => durableController.InitializeBindings(inputData));
        }

        // TODO: change the name of the method being tested to InitializeBindings
        [Theory]
        [InlineData(DurableFunctionType.None)]
        [InlineData(DurableFunctionType.ActivityFunction)]
        internal void BeforeFunctionInvocation_DoesNothing_ForNonOrchestrationFunction(DurableFunctionType durableFunctionType)
        {
            var durableController = CreateDurableController(durableFunctionType);
            var orchestrationContext = new OrchestrationContext { InstanceId = Guid.NewGuid().ToString() };

            var inputData = new[]
            {
                // Even if a parameter similar to orchestration context is passed:
                CreateParameterBinding("ParameterName", orchestrationContext)
            };

            durableController.InitializeBindings(inputData);
        }

        [Theory]
        [InlineData(DurableFunctionType.None)]
        [InlineData(DurableFunctionType.ActivityFunction)]
        [InlineData(DurableFunctionType.OrchestrationFunction)]
        internal void AfterFunctionInvocation_ClearsOrchestrationContext(DurableFunctionType durableFunctionType)
        {
            var durableController = CreateDurableController(durableFunctionType);
            _mockPowerShellServices.Setup(_ => _.ClearOrchestrationContext());

            durableController.AfterFunctionInvocation();

            _mockPowerShellServices.Verify(_ => _.ClearOrchestrationContext(), Times.Once);
        }

        [Fact]
        public void TryGetInputBindingParameterValue_RetrievesOrchestrationContextParameter_ForOrchestrationFunction()
        {
            var durableController = CreateDurableController(DurableFunctionType.OrchestrationFunction);

            var orchestrationContext = new OrchestrationContext { InstanceId = Guid.NewGuid().ToString() };
            const string contextParameterName = "ParameterName";
            var orchestrationBindingInfo = new OrchestrationBindingInfo(contextParameterName, orchestrationContext);
            var inputData = new[]
            {
                CreateParameterBinding(contextParameterName, orchestrationContext)
            };

            Action<object> externalInvoker;
            _mockPowerShellServices.Setup(_ => _.SetOrchestrationContext(It.IsAny<ParameterBinding>(), out externalInvoker)).Returns(orchestrationBindingInfo);
            durableController.InitializeBindings(inputData);

            Assert.True(durableController.TryGetInputBindingParameterValue(contextParameterName, out var value));
            Assert.Equal(orchestrationContext.InstanceId, ((OrchestrationContext)value).InstanceId);
        }

        [Theory]
        [InlineData(DurableFunctionType.None)]
        [InlineData(DurableFunctionType.ActivityFunction)]
        internal void TryGetInputBindingParameterValue_RetrievesNothing_ForNonOrchestrationFunction(DurableFunctionType durableFunctionType)
        {
            var durableController = CreateDurableController(durableFunctionType);

            var orchestrationContext = new OrchestrationContext { InstanceId = Guid.NewGuid().ToString() };
            const string contextParameterName = "ParameterName";
            var inputData = new[]
            {
                CreateParameterBinding(contextParameterName, orchestrationContext)
            };

            Action<object> externalInvoker;
            _mockPowerShellServices.Setup(_ => _.SetOrchestrationContext(It.IsAny<ParameterBinding>(), out externalInvoker));
            durableController.InitializeBindings(inputData);

            Assert.False(durableController.TryGetInputBindingParameterValue(contextParameterName, out var value));
            Assert.Null(value);
        }

        [Fact]
        public void TryInvokeOrchestrationFunction_InvokesOrchestrationFunction()
        {
            var contextParameterName = "ParameterName";
            var orchestrationContext = new OrchestrationContext { InstanceId = Guid.NewGuid().ToString() };
            var inputData = new[] { CreateParameterBinding(contextParameterName, orchestrationContext) };
            var orchestrationBindingInfo = new OrchestrationBindingInfo(contextParameterName, orchestrationContext);

            var durableController = CreateDurableController(DurableFunctionType.OrchestrationFunction);

            Action<object> externalInvoker;
            _mockPowerShellServices.Setup(_ => _.SetOrchestrationContext(It.IsAny<ParameterBinding>(), out externalInvoker)).Returns(orchestrationBindingInfo);
            durableController.InitializeBindings(inputData);

            var expectedResult = new Hashtable();
            _mockOrchestrationInvoker.Setup(
                _ => _.Invoke(It.IsAny<OrchestrationBindingInfo>(), It.IsAny<IPowerShellServices>()))
                .Returns(expectedResult);

            var actualResult = durableController.InvokeOrchestrationFunction();
            Assert.Same(expectedResult, actualResult);

            _mockOrchestrationInvoker.Verify(
                _ => _.Invoke(
                    It.Is<OrchestrationBindingInfo>(
                        bindingInfo => bindingInfo.Context.InstanceId == orchestrationContext.InstanceId
                                       && bindingInfo.ParameterName == contextParameterName),
                    _mockPowerShellServices.Object),
                Times.Once);
        }

        [Fact]
        public void AddPipelineOutputIfNecessary_AddsDollarReturn_ForActivityFunction()
        {
            var durableController = CreateDurableController(DurableFunctionType.ActivityFunction);

            var pipelineItems = new Collection<object> { "Item1", "Item2", "Item3" };
            var result = new Hashtable { { "FieldA", "ValueA" }, { "FieldB", "ValueB" } };
            var originalResultCount = result.Count;

            durableController.AddPipelineOutputIfNecessary(pipelineItems, result);

            Assert.Equal(originalResultCount + 1, result.Count);

            var dollarReturnValue = result[AzFunctionInfo.DollarReturn];

            Assert.Equal(
                (IEnumerable<object>)dollarReturnValue,
                FunctionReturnValueBuilder.CreateReturnValueFromFunctionOutput(pipelineItems));
        }

        [Theory]
        [InlineData(DurableFunctionType.None)]
        [InlineData(DurableFunctionType.OrchestrationFunction)]
        internal void AddPipelineOutputIfNecessary_DoesNotAddDollarReturn_ForNonActivityFunction(DurableFunctionType durableFunctionType)
        {
            var durableController = CreateDurableController(durableFunctionType);

            var pipelineItems = new Collection<object> { "Item1", "Item2", "Item3" };
            var result = new Hashtable { { "FieldA", "ValueA" }, { "FieldB", "ValueB" } };

            durableController.AddPipelineOutputIfNecessary(pipelineItems, result);

            Assert.False(result.ContainsKey(AzFunctionInfo.DollarReturn));
        }

        [Theory]
        [InlineData(DurableFunctionType.None, false)]
        [InlineData(DurableFunctionType.OrchestrationFunction, false)]
        [InlineData(DurableFunctionType.ActivityFunction, true)]
        internal void SuppressPipelineTracesForActivityFunctionOnly(DurableFunctionType durableFunctionType, bool shouldSuppressPipelineTraces)
        {
            var durableController = CreateDurableController(durableFunctionType);
            Assert.Equal(shouldSuppressPipelineTraces, durableController.ShouldSuppressPipelineTraces());
        }

        private DurableController CreateDurableController(
            DurableFunctionType durableFunctionType,
            string durableClientBindingName = null)
        {
            var durableFunctionInfo = new DurableFunctionInfo(durableFunctionType, durableClientBindingName);

            return new DurableController(
                            durableFunctionInfo,
                            _mockPowerShellServices.Object,
                            _mockOrchestrationInvoker.Object);
        }

        private static ParameterBinding CreateParameterBinding(string parameterName, object value)
        {
            return new ParameterBinding
            {
                Name = parameterName,
                Data = new TypedData
                {
                    String = JsonConvert.SerializeObject(value)
                }
            };
        }
    }
}
