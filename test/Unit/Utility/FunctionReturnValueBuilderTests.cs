//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

namespace Microsoft.Azure.Functions.PowerShellWorker.Test.Utility
{
    using PowerShellWorker.Utility;
    using Xunit;

    public class FunctionReturnValueBuilderTests
    {
        [Fact]
        public void ReturnsNullOnNull()
        {
            Assert.Null(FunctionReturnValueBuilder.CreateReturnValueFromFunctionOutput(null));
        }

        [Fact]
        public void ReturnsSingleItemWhenPipelineContainsSingleItem()
        {
            var pipelineItems = new[] { new object() };
            var returnValue = FunctionReturnValueBuilder.CreateReturnValueFromFunctionOutput(pipelineItems);
            Assert.Same(pipelineItems[0], returnValue);
        }

        [Fact]
        public void ReturnsNewArrayWhenPipelineContainsMultipleItems()
        {
            var pipelineItems = new[] { new object(), new object() };
            var returnValue = FunctionReturnValueBuilder.CreateReturnValueFromFunctionOutput(pipelineItems);
            Assert.IsType<object[]>(returnValue);
            Assert.NotSame(pipelineItems, returnValue);
            Assert.Same(pipelineItems[0], ((object[])returnValue)[0]);
            Assert.Same(pipelineItems[1], ((object[])returnValue)[1]);
            Assert.Equal(pipelineItems.Length, ((object[])returnValue).Length);
        }
    }
}
