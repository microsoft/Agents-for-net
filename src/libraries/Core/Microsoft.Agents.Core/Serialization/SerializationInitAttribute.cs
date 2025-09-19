// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;

namespace Microsoft.Agents.Core.Serialization
{
    [AttributeUsage(AttributeTargets.Assembly, AllowMultiple = true)]
    public class SerializationInitAttribute(Type type) : Attribute
    {
        public Type InitType = type;
    }
}
