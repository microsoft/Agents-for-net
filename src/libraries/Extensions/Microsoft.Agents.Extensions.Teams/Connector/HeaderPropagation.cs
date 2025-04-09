// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Generic;
using Microsoft.Agents.Core.HeaderPropagation;
using Microsoft.Extensions.Primitives;

namespace Microsoft.Agents.Extensions.Teams.Connector
{
    [HeaderPropagation]
    internal class HeaderPropagation
    {
        public static Dictionary<string, StringValues> SetHeaders()
        {
            // TODO: this functionality could be improved by providing a helper method to set the header either with a key or a key and value.
            // Possibly provide an interface or abstract class to guide customers on how to implement the SetHeaders method.
            return new Dictionary<string, StringValues>()
            {
                // 1. Provide a header without value to propagate the incoming request value to outgoing requests.
                // { "User-Agent", "" }

                // 2. Provide a header with value to propagate it to outgoing requests.
                //{ "User-Agent", "< VALUE-TO-PROPAGATE >" }
            };
        }
    }
}
