// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Text.Json.Nodes;

namespace Microsoft.Agents.Core.Serialization
{
    public static class TypeExtensions
    {
        public static void AddTypeInfo(this JsonObject jsonObject, object value)
        {
            jsonObject["$type"] = value.GetType().FullName;
            jsonObject["$typeAssembly"] = value.GetType().Assembly.GetName().Name;
        }

        public static bool GetTypeInfo(this JsonObject jsonObject, out Type type)
        {
            if (jsonObject.ContainsKey("$type"))
            {
                var assembly = AppDomain.CurrentDomain.Load(jsonObject["$typeAssembly"].ToString().Trim());
                type = assembly.GetType(jsonObject["$type"].ToString().Trim());
                return type != null;
            }

            type = null;
            return false;
        }

        public static void RemoveTypeInfo(this JsonObject jsonObject)
        {
            jsonObject.Remove("$type");
            jsonObject.Remove("$typeAssembly");
        }
        public static void RemoveTypeInfo(this IDictionary<string, object> dict)
        {
            dict.Remove("$type");
            dict.Remove("$typeAssembly");
        }

        public static IDictionary<string, string> GetTypeInfoProperties(this JsonObject jsonObject)
        {
            return new Dictionary<string, string>
            {
                { "$type", jsonObject["$type"].ToString() },
                { "$typeAssembly", jsonObject["$typeAssembly"].ToString() }
            };
        }

        public static IDictionary<string, string> RemoveTypeInfoProperties(this JsonObject jsonObject)
        {
            var props = GetTypeInfoProperties(jsonObject);
            jsonObject.RemoveTypeInfo();
            return props;
        }

        public static void SetTypeInfoProperties(this JsonObject jsonObject, IDictionary<string, string> properties)
        {
            jsonObject["$type"] = properties["$type"];
            jsonObject["$typeAssembly"] = properties["$typeAssembly"];
        }
    }
}
