//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System;

namespace Microsoft.Azure.Functions.PowerShellWorker.Attributes
{
    /// <summary>
    /// Base class for all input binding attributes in the V2 programming model.
    /// </summary>
    [AttributeUsage(AttributeTargets.Parameter, AllowMultiple = false)]
    public abstract class InputBindingBaseAttribute : Attribute
    {
        /// <summary>
        /// The binding type string for function.json (e.g., "cosmosDB", "blob").
        /// Each derived class must return its binding type.
        /// </summary>
        public abstract string BindingType { get; }

        /// <summary>
        /// The connection string setting name for this binding.
        /// </summary>
        public string Connection { get; set; }

        /// <summary>
        /// Comma-delimited list of property names that should always be serialized as JSON arrays.
        /// </summary>
        public virtual string ArrayProperties => null;

        /// <summary>
        /// Comma-delimited list of property names that are required for this binding.
        /// Override in derived classes to enable indexing-time validation.
        /// </summary>
        public virtual string RequiredProperties => null;
    }
}
