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
                type = GetType(jsonObject);
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
            var type = GetType(jsonObject);
            return new Dictionary<string, string>
            {
                { "$type", type?.FullName },
                { "$typeAssembly", type.Assembly.FullName }
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

        private static Type GetType(JsonObject jsonObject)
        {
            string assemblyName;
            var typeName = jsonObject["$type"]?.ToString()?.Trim();
            if (string.IsNullOrEmpty(typeName))
            {
                return null;
            }

            var assemblyProperty = jsonObject["$typeAssembly"];
            if (assemblyProperty != null)
            {
                assemblyName = assemblyProperty.ToString().Trim();
            }
            else
            {
                var split = typeName.ToString().Split(',');
                if (split.Length > 1)
                {
                    typeName = split[0].Trim();
                    assemblyName = split[1].Trim();
                }
                else
                {
                    return null;
                }
            }

            var assembly = AppDomain.CurrentDomain.Load(assemblyName);
            return assembly?.GetType(typeName);
        }
    }
}
