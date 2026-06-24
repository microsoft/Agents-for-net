// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text.Json.Nodes;

namespace Microsoft.Agents.Core.Serialization
{
    public static class TypeExtensions
    {
        public static void AddTypeInfo(this JsonObject jsonObject, object value)
        {
            Type type = value.GetType();
            jsonObject["$type"] = type.FullName;
            jsonObject["$typeAssembly"] = GetAssemblySimpleName(type.Assembly);
        }

        public static void AddTypeInfo(this JsonNode jsonNode, object value)
        {
            if (jsonNode is JsonObject jsonObject)
            {
                jsonObject.AddTypeInfo(value);
            }
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

        public static bool GetTypeInfo(this JsonNode jsonNode, out Type type)
        {
            if (jsonNode is JsonObject jsonObject)
            {
                return jsonObject.GetTypeInfo(out type);
            }

            type = null;
            return false;
        }

        public static void RemoveTypeInfo(this JsonObject jsonObject)
        {
            jsonObject.Remove("$type");
            jsonObject.Remove("$typeAssembly");
        }

        public static void RemoveTypeInfo(this JsonNode jsonNode)
        {
            if (jsonNode is JsonObject jsonObject)
            {
                jsonObject.RemoveTypeInfo();
            }
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

        public static void AddCollectionTypeInfo(this JsonNode jsonNode, Type collectionType)
        {
            jsonNode.AsObject()["$collectionType"] = collectionType.FullName;
            jsonNode.AsObject()["$collectionTypeAssembly"] = GetAssemblySimpleName(collectionType.Assembly);
        }

        public static void RemoveCollectionTypeInfo(this JsonNode jsonNode)
        {
            jsonNode.AsObject().Remove("$collectionType");
            jsonNode.AsObject().Remove("$collectionTypeAssembly");
        }

        public static Type GetCollectionTypeInfo(this JsonArray jsonArray)
        {
            if (jsonArray.Count == 0)
            {
                return typeof(object);
            }

            if (!jsonArray[0].AsObject().ContainsKey("$collectionType"))
            {
                return typeof(object);
            }

            var assembly = AppDomain.CurrentDomain.Load((string)jsonArray[0].AsObject()["$collectionTypeAssembly"]);
            return assembly?.GetType((string)jsonArray[0].AsObject()["$collectionType"]) ?? typeof(object);
        }

        // Extract the simple assembly name from Assembly.FullName without calling
        // Assembly.GetName(), which internally invokes CultureInfo.GetCultureInfo()
        // and can throw CultureNotFoundException for assemblies with invalid or
        // synthetic culture metadata (e.g. runtime-generated generic type assemblies).
        private static string GetAssemblySimpleName(Assembly assembly)
        {
            int commaIndex = assembly.FullName.IndexOf(',');
            return commaIndex > 0 ? assembly.FullName.Substring(0, commaIndex) : assembly.FullName;
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
