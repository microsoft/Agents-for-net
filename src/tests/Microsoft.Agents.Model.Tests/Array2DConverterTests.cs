// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Core.Serialization;
using System.Collections.Generic;
using System.Text.Json;
using Xunit;

namespace Microsoft.Agents.Model.Tests
{
    /// <summary>
    /// Tests for Array2DConverter which handles serialization/deserialization
    /// of 2D arrays (T[,]) as jagged JSON arrays (array of arrays).
    /// The converter is registered in ProtocolJsonSerializer and fires when the
    /// target type is known to be a 2D array.
    /// </summary>
    public class Array2DConverterTests
    {
        private static readonly JsonSerializerOptions SdkOptions = ProtocolJsonSerializer.SerializationOptions;

        #region Int 2D arrays

        [Fact]
        public void Roundtrip_Int2DArray_PreservesValues()
        {
            var original = new int[,] { { 1, 2, 3 }, { 4, 5, 6 } };

            var json = JsonSerializer.Serialize(original, SdkOptions);
            var deserialized = JsonSerializer.Deserialize<int[,]>(json, SdkOptions);

            Assert.Equal(2, deserialized.GetLength(0));
            Assert.Equal(3, deserialized.GetLength(1));
            Assert.Equal(1, deserialized[0, 0]);
            Assert.Equal(3, deserialized[0, 2]);
            Assert.Equal(4, deserialized[1, 0]);
            Assert.Equal(6, deserialized[1, 2]);
        }

        [Fact]
        public void Serialize_Int2DArray_ProducesJaggedJson()
        {
            var array = new int[,] { { 1, 2 }, { 3, 4 } };

            var json = JsonSerializer.Serialize(array, SdkOptions);

            Assert.Equal("[[1,2],[3,4]]", json);
        }

        #endregion

        #region String 2D arrays

        [Fact]
        public void Roundtrip_String2DArray_PreservesValues()
        {
            var original = new string[,] { { "a", "b" }, { "c", "d" } };

            var json = JsonSerializer.Serialize(original, SdkOptions);
            var deserialized = JsonSerializer.Deserialize<string[,]>(json, SdkOptions);

            Assert.Equal(2, deserialized.GetLength(0));
            Assert.Equal(2, deserialized.GetLength(1));
            Assert.Equal("a", deserialized[0, 0]);
            Assert.Equal("b", deserialized[0, 1]);
            Assert.Equal("c", deserialized[1, 0]);
            Assert.Equal("d", deserialized[1, 1]);
        }

        #endregion

        #region Bool 2D arrays

        [Fact]
        public void Roundtrip_Bool2DArray_PreservesValues()
        {
            var original = new bool[,] { { true, false }, { false, true } };

            var json = JsonSerializer.Serialize(original, SdkOptions);
            var deserialized = JsonSerializer.Deserialize<bool[,]>(json, SdkOptions);

            Assert.Equal(2, deserialized.GetLength(0));
            Assert.Equal(2, deserialized.GetLength(1));
            Assert.True(deserialized[0, 0]);
            Assert.False(deserialized[0, 1]);
            Assert.False(deserialized[1, 0]);
            Assert.True(deserialized[1, 1]);
        }

        #endregion

        #region Edge cases

        [Fact]
        public void Roundtrip_SingleElement2DArray_PreservesValue()
        {
            var original = new int[,] { { 42 } };

            var json = JsonSerializer.Serialize(original, SdkOptions);
            var deserialized = JsonSerializer.Deserialize<int[,]>(json, SdkOptions);

            Assert.Equal(1, deserialized.GetLength(0));
            Assert.Equal(1, deserialized.GetLength(1));
            Assert.Equal(42, deserialized[0, 0]);
        }

        [Fact]
        public void Roundtrip_LargerMatrix_PreservesAllValues()
        {
            var original = new int[4, 5];
            for (int i = 0; i < 4; i++)
                for (int j = 0; j < 5; j++)
                    original[i, j] = i * 10 + j;

            var json = JsonSerializer.Serialize(original, SdkOptions);
            var deserialized = JsonSerializer.Deserialize<int[,]>(json, SdkOptions);

            Assert.Equal(4, deserialized.GetLength(0));
            Assert.Equal(5, deserialized.GetLength(1));
            Assert.Equal(0, deserialized[0, 0]);
            Assert.Equal(14, deserialized[1, 4]);
            Assert.Equal(34, deserialized[3, 4]);
        }

        [Fact]
        public void Roundtrip_StronglyTypedContainer_PreservesMatrix()
        {
            var container = new MatrixContainer
            {
                Name = "test",
                Matrix = new int[,] { { 10, 20 }, { 30, 40 } }
            };

            var json = JsonSerializer.Serialize(container, SdkOptions);
            var deserialized = JsonSerializer.Deserialize<MatrixContainer>(json, SdkOptions);

            Assert.Equal("test", deserialized.Name);
            Assert.Equal(2, deserialized.Matrix.GetLength(0));
            Assert.Equal(2, deserialized.Matrix.GetLength(1));
            Assert.Equal(10, deserialized.Matrix[0, 0]);
            Assert.Equal(40, deserialized.Matrix[1, 1]);
        }

        [Fact]
        public void Serialize_2DArray_InDictionary_DoesNotCrash()
        {
            // 2D arrays stored in IDictionary<string, object> should serialize
            // without throwing (even though roundtrip back to T[,] isn't supported
            // through untyped dictionary values).
            var dictionary = new Dictionary<string, object>
            {
                ["matrix"] = new int[,] { { 1, 2 }, { 3, 4 } }
            };

            var json = JsonSerializer.Serialize<IDictionary<string, object>>(dictionary, SdkOptions);

            Assert.Contains("[[1,2],[3,4]]", json);
        }

        #endregion
    }

    class MatrixContainer
    {
        public string Name { get; set; }
        public int[,] Matrix { get; set; }
    }
}
