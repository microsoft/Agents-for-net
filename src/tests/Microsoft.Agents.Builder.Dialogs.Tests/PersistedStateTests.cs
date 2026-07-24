// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Core.Serialization;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;
using Xunit;

namespace Microsoft.Agents.Builder.Dialogs.Tests
{
    /// <summary>
    /// Tests for PersistedStateConverter serialization/deserialization of primitive arrays
    /// and complex types within PersistedState (IDictionary&lt;string, object&gt;).
    /// Validates that arrays of primitive types (string[], int[], bool[]) can be
    /// serialized and round-tripped through ProtocolJsonSerializer.
    /// </summary>
    public class PersistedStateTests
    {
        #region Polymorphic type round-trips

        [Fact]
        public void PersistedState_ListRoundTrip()
        {
            var state = new TestState();
            state.State["block"] = new List<BaseBlock> { new SubBlock { Id = "1", Data = "Test" } };

            var json = ProtocolJsonSerializer.ToJson(state);

            var deserializedState = ProtocolJsonSerializer.ToObject<TestState>(json);

            Assert.IsAssignableFrom<List<BaseBlock>>(deserializedState.State["block"]);
        }

        [Fact]
        public void PersistedState_ArrayRoundTrip()
        {
            var state = new TestState();
            state.State["block"] = new BaseBlock[] { new SubBlock { Id = "1", Data = "Test" } };

            var json = ProtocolJsonSerializer.ToJson(state);

            var deserializedState = ProtocolJsonSerializer.ToObject<TestState>(json);

            Assert.IsAssignableFrom<BaseBlock[]>(deserializedState.State["block"]);
        }

        #endregion

        #region Primitive arrays serialization

        [Fact]
        public void Serialize_StringArray_Succeeds()
        {
            var state = new TestState();
            state.State["parameters"] = new string[] { "param1", "param2", "param3" };

            var json = ProtocolJsonSerializer.ToJson(state);

            Assert.Contains("param1", json);
            Assert.Contains("param2", json);
            Assert.Contains("param3", json);
        }

        [Fact]
        public void Serialize_IntArray_Succeeds()
        {
            var state = new TestState();
            state.State["counters"] = new int[] { 1, 2, 3 };

            var json = ProtocolJsonSerializer.ToJson(state);

            Assert.Contains("1", json);
            Assert.Contains("2", json);
            Assert.Contains("3", json);
        }

        [Fact]
        public void Serialize_BoolArray_Succeeds()
        {
            var state = new TestState();
            state.State["flags"] = new bool[] { true, false, true };

            var json = ProtocolJsonSerializer.ToJson(state);

            Assert.Contains("true", json);
            Assert.Contains("false", json);
        }

        #endregion

        #region WaterfallDialog state simulation

        [Fact]
        public void Serialize_WaterfallState_WithStringArrayOptions_Succeeds()
        {
            var state = new TestState();
            state.State["options"] = new string[] { "AddressNormalizerResponse", "AzureMaps" };
            state.State["values"] = new PersistedState();
            state.State["instanceId"] = "4db4c090-cd48-4682-9810-b6590f2bd72f";
            state.State["stepIndex"] = 0;

            var json = ProtocolJsonSerializer.ToJson(state);

            Assert.Contains("options", json);
            Assert.Contains("AddressNormalizerResponse", json);
            Assert.Contains("AzureMaps", json);
        }

        [Fact]
        public void Serialize_WaterfallState_WithStringArrayInValues_Succeeds()
        {
            var values = new PersistedState();
            values["ApiParameters"] = new string[] { "param1", "param2", "param3" };

            var state = new TestState();
            state.State["options"] = null;
            state.State["values"] = values;
            state.State["instanceId"] = "test-instance-id";
            state.State["stepIndex"] = 0;

            var json = ProtocolJsonSerializer.ToJson(state);

            Assert.Contains("ApiParameters", json);
        }

        [Fact]
        public void Serialize_WaterfallState_WithStringArrayInBothOptionsAndValues_Succeeds()
        {
            var values = new PersistedState();
            values["ApiParameters"] = new string[] { "AddressNormalizerResponse", "GoogleMaps", "{Street}" };

            var state = new TestState();
            state.State["options"] = new string[] { "AddressNormalizerResponse", "GoogleMaps" };
            state.State["values"] = values;
            state.State["instanceId"] = "test-instance-id";
            state.State["stepIndex"] = 0;

            var json = ProtocolJsonSerializer.ToJson(state);

            Assert.Contains("options", json);
            Assert.Contains("ApiParameters", json);
        }

        #endregion

        #region Roundtrip: primitive arrays

        [Fact]
        public void Roundtrip_StringArray_PreservesValues()
        {
            var state = new TestState();
            state.State["items"] = new string[] { "alpha", "beta", "gamma" };

            var json = ProtocolJsonSerializer.ToJson(state);
            var deserialized = ProtocolJsonSerializer.ToObject<TestState>(json);

            Assert.NotNull(deserialized);
            Assert.True(deserialized.State.ContainsKey("items"));
            IList items = (IList)deserialized.State["items"];
            Assert.Equal(3, items.Count);
            Assert.Equal("alpha", items[0].ToString());
            Assert.Equal("beta", items[1].ToString());
            Assert.Equal("gamma", items[2].ToString());
        }

        [Fact]
        public void Roundtrip_IntArray_PreservesValues()
        {
            var state = new TestState();
            state.State["counters"] = new int[] { 10, 20, 30 };

            var json = ProtocolJsonSerializer.ToJson(state);
            var deserialized = ProtocolJsonSerializer.ToObject<TestState>(json);

            Assert.NotNull(deserialized);
            Assert.True(deserialized.State.ContainsKey("counters"));
            IList counters = (IList)deserialized.State["counters"];
            Assert.Equal(3, counters.Count);
            Assert.Equal(10, Convert.ToInt32(counters[0]));
            Assert.Equal(20, Convert.ToInt32(counters[1]));
            Assert.Equal(30, Convert.ToInt32(counters[2]));
        }

        [Fact]
        public void Roundtrip_WaterfallState_WithStringArray_PreservesStructure()
        {
            var state = new TestState();
            state.State["options"] = new string[] { "AddressNormalizerResponse", "AzureMaps" };
            state.State["instanceId"] = "test-id";
            state.State["stepIndex"] = 0;

            var json = ProtocolJsonSerializer.ToJson(state);
            var deserialized = ProtocolJsonSerializer.ToObject<TestState>(json);

            Assert.NotNull(deserialized);
            IList options = (IList)deserialized.State["options"];
            Assert.Equal(2, options.Count);
            Assert.Equal("AddressNormalizerResponse", options[0].ToString());
            Assert.Equal("AzureMaps", options[1].ToString());
            Assert.Equal("test-id", deserialized.State["instanceId"].ToString());
        }

        [Fact]
        public void Roundtrip_MixedArray_PreservesTypes()
        {
            var state = new TestState();
            state.State["mixed"] = new object[] { "text", 100, true };

            var json = ProtocolJsonSerializer.ToJson(state);
            var deserialized = ProtocolJsonSerializer.ToObject<TestState>(json);

            Assert.NotNull(deserialized);
            Assert.True(deserialized.State.ContainsKey("mixed"));
            IList mixed = (IList)deserialized.State["mixed"];
            Assert.Equal(3, mixed.Count);
            Assert.Equal("text", mixed[0].ToString());
            Assert.Equal(100, Convert.ToInt32(mixed[1]));
            Assert.True(Convert.ToBoolean(mixed[2]));
        }

        #endregion

        #region Typed array deserialization (string[], int[], bool[])

        [Fact]
        public void Roundtrip_StringArray_DeserializesToStringArray()
        {
            var state = new TestState();
            state.State["parameters"] = new string[] { "AddressNormalizerResponse", "AzureMaps", "{Street}" };

            var json = ProtocolJsonSerializer.ToJson(state);
            var deserialized = ProtocolJsonSerializer.ToObject<TestState>(json);

            Assert.NotNull(deserialized);
            Assert.IsType<string[]>(deserialized.State["parameters"]);
            string[] parameters = (string[])deserialized.State["parameters"];
            Assert.Equal(3, parameters.Length);
            Assert.Equal("AddressNormalizerResponse", parameters[0]);
            Assert.Equal("AzureMaps", parameters[1]);
            Assert.Equal("{Street}", parameters[2]);
        }

        [Fact]
        public void Roundtrip_IntArray_DeserializesToIntArray()
        {
            var state = new TestState();
            state.State["counters"] = new int[] { 10, 20, 30 };

            var json = ProtocolJsonSerializer.ToJson(state);
            var deserialized = ProtocolJsonSerializer.ToObject<TestState>(json);

            Assert.NotNull(deserialized);
            Assert.IsType<int[]>(deserialized.State["counters"]);
            int[] counters = (int[])deserialized.State["counters"];
            Assert.Equal(3, counters.Length);
            Assert.Equal(10, counters[0]);
            Assert.Equal(20, counters[1]);
            Assert.Equal(30, counters[2]);
        }

        [Fact]
        public void Roundtrip_BoolArray_DeserializesToBoolArray()
        {
            var state = new TestState();
            state.State["flags"] = new bool[] { true, false, true };

            var json = ProtocolJsonSerializer.ToJson(state);
            var deserialized = ProtocolJsonSerializer.ToObject<TestState>(json);

            Assert.NotNull(deserialized);
            Assert.IsType<bool[]>(deserialized.State["flags"]);
            bool[] flags = (bool[])deserialized.State["flags"];
            Assert.Equal(3, flags.Length);
            Assert.True(flags[0]);
            Assert.False(flags[1]);
            Assert.True(flags[2]);
        }

        [Fact]
        public void Roundtrip_WaterfallValues_StringArray_DeserializesToStringArray()
        {
            // Simulates the WaterfallDialog multi-step pattern:
            // Step 1 stores string[] in stepContext.Values, state is serialized to storage.
            // Step 2 loads state, reads stepContext.Values["ApiParameters"].
            var values = new PersistedState();
            values["ApiParameters"] = new string[] { "AddressNormalizerResponse", "AzureMaps", "{Street}" };

            var state = new TestState();
            state.State["options"] = null;
            state.State["values"] = values;
            state.State["instanceId"] = "test-id";
            state.State["stepIndex"] = 1;

            var json = ProtocolJsonSerializer.ToJson(state);
            var deserialized = ProtocolJsonSerializer.ToObject<TestState>(json);

            Assert.NotNull(deserialized);
            IDictionary<string, object> deserializedValues = (IDictionary<string, object>)deserialized.State["values"];
            Assert.IsType<string[]>(deserializedValues["ApiParameters"]);
            string[] parameters = (string[])deserializedValues["ApiParameters"];
            Assert.Equal(3, parameters.Length);
            Assert.Equal("AddressNormalizerResponse", parameters[0]);
            Assert.Equal("AzureMaps", parameters[1]);
            Assert.Equal("{Street}", parameters[2]);
        }

        #endregion

        #region Arrays with null elements

        [Fact]
        public void Serialize_StringArrayWithNulls_Succeeds()
        {
            var state = new TestState();
            state.State["items"] = new string[] { "first", null, "third", null, "fifth" };

            var json = ProtocolJsonSerializer.ToJson(state);

            Assert.Contains("first", json);
            Assert.Contains("null", json);
            Assert.Contains("third", json);
            Assert.Contains("fifth", json);
        }

        [Fact]
        public void Roundtrip_StringArrayWithNulls_PreservesValues()
        {
            var state = new TestState();
            state.State["items"] = new string[] { "value1", null, "value2" };

            var json = ProtocolJsonSerializer.ToJson(state);
            var deserialized = ProtocolJsonSerializer.ToObject<TestState>(json);

            Assert.NotNull(deserialized);
            Assert.True(deserialized.State.ContainsKey("items"));
            IList items = (IList)deserialized.State["items"];
            Assert.Equal(3, items.Count);
            Assert.Equal("value1", items[0]?.ToString());
            Assert.Null(items[1]);
            Assert.Equal("value2", items[2]?.ToString());
        }

        #endregion

        #region Mixed content dictionaries

        [Fact]
        public void Serialize_MixedDictionary_WithPrimitivesObjectsAndArrays_Succeeds()
        {
            var state = new TestState();
            state.State["stringVal"] = "hello";
            state.State["intVal"] = 42;
            state.State["boolVal"] = true;
            state.State["nullVal"] = null;
            state.State["stringArray"] = new string[] { "a", "b", "c" };
            state.State["intArray"] = new int[] { 1, 2, 3 };

            var json = ProtocolJsonSerializer.ToJson(state);

            Assert.Contains("hello", json);
            Assert.Contains("42", json);
            Assert.Contains("true", json);
            Assert.Contains("\"a\"", json);
        }

        #endregion

        #region Empty arrays

        [Fact]
        public void Serialize_EmptyStringArray_Succeeds()
        {
            var state = new TestState();
            state.State["empty"] = Array.Empty<string>();

            var json = ProtocolJsonSerializer.ToJson(state);

            Assert.Contains("empty", json);
        }

        #endregion

        #region Large arrays

        [Fact]
        public void Serialize_LargeStringArray_Succeeds()
        {
            var largeArray = new string[1000];
            for (int i = 0; i < largeArray.Length; i++)
            {
                largeArray[i] = $"item_{i}";
            }

            var state = new TestState();
            state.State["largeArray"] = largeArray;

            var json = ProtocolJsonSerializer.ToJson(state);

            Assert.Contains("item_0", json);
            Assert.Contains("item_500", json);
            Assert.Contains("item_999", json);
        }

        [Fact]
        public void Roundtrip_LargeIntArray_PreservesAllValues()
        {
            var largeArray = new int[500];
            for (int i = 0; i < largeArray.Length; i++)
            {
                largeArray[i] = i * 2;
            }

            var state = new TestState();
            state.State["numbers"] = largeArray;

            var json = ProtocolJsonSerializer.ToJson(state);
            var deserialized = ProtocolJsonSerializer.ToObject<TestState>(json);

            Assert.NotNull(deserialized);
            IList numbers = (IList)deserialized.State["numbers"];
            Assert.Equal(500, numbers.Count);
            Assert.Equal(0, Convert.ToInt32(numbers[0]));
            Assert.Equal(250, Convert.ToInt32(numbers[125]));
            Assert.Equal(998, Convert.ToInt32(numbers[499]));
        }

        #endregion

        #region Complex real-world scenarios

        [Fact]
        public void Serialize_ComplexDialogState_WithMultipleArrayTypes_Succeeds()
        {
            var metadata = new PersistedState();
            metadata["tags"] = new string[] { "important", "urgent" };
            metadata["priorities"] = new int[] { 1, 2, 3 };

            var state = new TestState();
            state.State["dialogId"] = "multi-step-dialog";
            state.State["instanceId"] = "instance-123";
            state.State["stepIndex"] = 2;
            state.State["options"] = new string[] { "Option1", "Option2", "Option3" };
            state.State["previousResponses"] = new string[] { "response1", "response2" };
            state.State["scores"] = new int[] { 85, 92, 78 };
            state.State["flags"] = new bool[] { true, false, true, true };
            state.State["metadata"] = metadata;

            var json = ProtocolJsonSerializer.ToJson(state);

            Assert.Contains("multi-step-dialog", json);
            Assert.Contains("Option1", json);
            Assert.Contains("response1", json);
            Assert.Contains("85", json);
            Assert.Contains("important", json);
        }

        [Fact]
        public void Roundtrip_ComplexDialogState_PreservesAllData()
        {
            var state = new TestState();
            state.State["dialogId"] = "test-dialog";
            state.State["options"] = new string[] { "A", "B", "C" };
            state.State["scores"] = new int[] { 10, 20, 30 };
            state.State["enabled"] = new bool[] { true, false };

            var json = ProtocolJsonSerializer.ToJson(state);
            var deserialized = ProtocolJsonSerializer.ToObject<TestState>(json);

            Assert.NotNull(deserialized);
            Assert.Equal("test-dialog", deserialized.State["dialogId"].ToString());

            IList options = (IList)deserialized.State["options"];
            Assert.Equal(3, options.Count);
            Assert.Equal("A", options[0].ToString());

            IList scores = (IList)deserialized.State["scores"];
            Assert.Equal(3, scores.Count);
            Assert.Equal(10, Convert.ToInt32(scores[0]));

            IList enabled = (IList)deserialized.State["enabled"];
            Assert.Equal(2, enabled.Count);
            Assert.True(Convert.ToBoolean(enabled[0]));
            Assert.False(Convert.ToBoolean(enabled[1]));
        }

        #endregion

        #region Additional primitive types

        [Fact]
        public void Serialize_DoubleArray_Succeeds()
        {
            var state = new TestState();
            state.State["decimals"] = new double[] { 1.5, 2.7, 3.14 };

            var json = ProtocolJsonSerializer.ToJson(state);

            Assert.Contains("1.5", json);
            Assert.Contains("2.7", json);
            Assert.Contains("3.14", json);
        }

        [Fact]
        public void Serialize_LongArray_Succeeds()
        {
            var state = new TestState();
            state.State["bigNumbers"] = new long[] { 1000000000L, 2000000000L, 3000000000L };

            var json = ProtocolJsonSerializer.ToJson(state);

            Assert.Contains("1000000000", json);
            Assert.Contains("2000000000", json);
            Assert.Contains("3000000000", json);
        }

        #endregion
    }

    class TestState
    {
        public PersistedState State { get; set; } = [];
    }

    [JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
    [JsonDerivedType(typeof(SubBlock), "SUB")]
    public class BaseBlock
    {
        [JsonPropertyName("id")] public string Id { get; set; }
    }

    public class SubBlock : BaseBlock
    {
        [JsonPropertyName("data")] public string Data { get; set; }
    }
}
