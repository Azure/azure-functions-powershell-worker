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
        private const string _contextParameterName = "ParameterName";
        private static readonly OrchestrationContext _orchestrationContext = new OrchestrationContext { InstanceId = Guid.NewGuid().ToString() };
        private static readonly OrchestrationBindingInfo _orchestrationBindingInfo = new OrchestrationBindingInfo(_contextParameterName, _orchestrationContext);

        [Fact]
        public void InitializeBindings_SetsDurableClient_ForDurableClientFunction()
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
            _mockPowerShellServices.Setup(_ => _.HasExternalDurableSDK()).Returns(false);

            durableController.InitializeBindings(inputData, out bool hasExternalSDK);

            _mockPowerShellServices.Verify(
                _ => _.SetDurableClient(
                    It.Is<object>(c => (string)((Hashtable)c)["FakeClientProperty"] == durableClient.FakeClientProperty)),
                Times.Once);
        }

        [Fact]
        public void InitializeBindings_SetsOrchestrationContext_ForOrchestrationFunction()
        {
            var durableController = CreateDurableController(DurableFunctionType.OrchestrationFunction);
            var inputData = new[]
            {
                CreateParameterBinding("ParameterName", _orchestrationContext)
            };
            
            _mockPowerShellServices.Setup(_ => _.SetOrchestrationContext(It.IsAny<ParameterBinding>(),
                out It.Ref<IExternalOrchestrationInvoker>.IsAny))
            .Returns(_orchestrationBindingInfo);
            _mockOrchestrationInvoker.Setup(_ => _.SetExternalInvoker(It.IsAny<IExternalOrchestrationInvoker>()));
            _mockPowerShellServices.Setup(_ => _.HasExternalDurableSDK()).Returns(false);

            durableController.InitializeBindings(inputData, out bool hasExternalSDK);

            _mockPowerShellServices.Verify(
                _ => _.SetOrchestrationContext(
                    It.Is<ParameterBinding>(c => c.Data.ToString().Contains(_orchestrationContext.InstanceId)),
                        out It.Ref<IExternalOrchestrationInvoker>.IsAny),
                Times.Once);
        }

        [Fact]
        public void InitializeBindings_Throws_OnOrchestrationFunctionWithoutContextParameter()
        {
            var durableController = CreateDurableController(DurableFunctionType.OrchestrationFunction);
            var inputData = new ParameterBinding[0];

            Assert.ThrowsAny<ArgumentException>(() => durableController.InitializeBindings(inputData, out bool hasExternalSDK));
        }

        [Theory]
        [InlineData(DurableFunctionType.None)]
        [InlineData(DurableFunctionType.ActivityFunction)]
        internal void InitializeBindings_DoesNothing_ForNonOrchestrationFunction(DurableFunctionType durableFunctionType)
        {
            var durableController = CreateDurableController(durableFunctionType);
            var inputData = new[]
            {
                // Even if a parameter similar to orchestration context is passed:
                CreateParameterBinding("ParameterName", _orchestrationContext)
            };

            durableController.InitializeBindings(inputData, out bool hasExternalSDK);
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
            var inputData = new[]
            {
                CreateParameterBinding(_contextParameterName, _orchestrationContext)
            };
            _mockPowerShellServices.Setup(_ => _.SetOrchestrationContext(
                It.IsAny<ParameterBinding>(),
                out It.Ref<IExternalOrchestrationInvoker>.IsAny))
            .Returns(_orchestrationBindingInfo);
            _mockPowerShellServices.Setup(_ => _.HasExternalDurableSDK()).Returns(false);

            _mockOrchestrationInvoker.Setup(_ => _.SetExternalInvoker(It.IsAny<IExternalOrchestrationInvoker>()));
            durableController.InitializeBindings(inputData, out bool hasExternalSDK);

            Assert.True(durableController.TryGetInputBindingParameterValue(_contextParameterName, out var value));
            Assert.Equal(_orchestrationContext.InstanceId, ((OrchestrationContext)value).InstanceId);
        }

        [Theory]
        [InlineData(DurableFunctionType.None)]
        [InlineData(DurableFunctionType.ActivityFunction)]
        internal void TryGetInputBindingParameterValue_RetrievesNothing_ForNonOrchestrationFunction(DurableFunctionType durableFunctionType)
        {
            var durableController = CreateDurableController(durableFunctionType);
            var inputData = new[]
            {
                CreateParameterBinding(_contextParameterName, _orchestrationContext)
            };

            _mockPowerShellServices.Setup(_ => _.SetOrchestrationContext(
                It.IsAny<ParameterBinding>(),
                out It.Ref<IExternalOrchestrationInvoker>.IsAny))
            .Returns(_orchestrationBindingInfo);
            _mockPowerShellServices.Setup(_ => _.HasExternalDurableSDK()).Returns(false);

            _mockOrchestrationInvoker.Setup(_ => _.SetExternalInvoker(It.IsAny<IExternalOrchestrationInvoker>()));
            durableController.InitializeBindings(inputData, out bool hasExternalSDK);

            Assert.False(durableController.TryGetInputBindingParameterValue(_contextParameterName, out var value));
            Assert.Null(value);
        }

        [Fact]
        public void TryInvokeOrchestrationFunction_InvokesOrchestrationFunction()
        {
            var inputData = new[] { CreateParameterBinding(_contextParameterName, _orchestrationContext) };
            var durableController = CreateDurableController(DurableFunctionType.OrchestrationFunction);

            _mockPowerShellServices.Setup(_ => _.SetOrchestrationContext(
                It.IsAny<ParameterBinding>(),
                out It.Ref<IExternalOrchestrationInvoker>.IsAny))
            .Returns(_orchestrationBindingInfo);
            _mockPowerShellServices.Setup(_ => _.HasExternalDurableSDK()).Returns(false);

            _mockOrchestrationInvoker.Setup(_ => _.SetExternalInvoker(It.IsAny<IExternalOrchestrationInvoker>()));

            durableController.InitializeBindings(inputData, out bool hasExternalSDK);

            var expectedResult = new Hashtable();
            _mockOrchestrationInvoker.Setup(
                _ => _.Invoke(It.IsAny<OrchestrationBindingInfo>(), It.IsAny<IPowerShellServices>()))
                .Returns(expectedResult);

            var actualResult = durableController.InvokeOrchestrationFunction();
            Assert.Same(expectedResult, actualResult);

            _mockOrchestrationInvoker.Verify(
                _ => _.Invoke(
                    It.Is<OrchestrationBindingInfo>(
                        bindingInfo => bindingInfo.Context.InstanceId == _orchestrationContext.InstanceId
                                       && bindingInfo.ParameterName == _contextParameterName),
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
