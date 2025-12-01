// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace Microsoft.Agents.Core.Models.Entities
{
    public class ProductInfo :Entity
    {
        public ProductInfo() : base(EntityTypes.ProductInfo)
        {
        }

        public string Id { get; set; }
    }
}
