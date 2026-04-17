#pragma warning disable SA1402

using Microsoft.Agents.Builder.State;
using Microsoft.Agents.Core.Serialization;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using Xunit;
using Xunit.Sdk;

namespace Microsoft.Agents.State.Tests
{
    public class ObjectPathTests
    {
        [Fact]
        public void Typed_OnlyDefaultTest()
        {
            var defaultOptions = new Options()
            {
                LastName = "Smith",
                FirstName = "Fred",
                Age = 22,
                Location = new Location() { Lat = 1.2312312F, Long = 3.234234F }
            };
            var overlay = new Options() { };

            var result = ObjectPath.Merge(defaultOptions, overlay);
            Assert.Equal(result.LastName, defaultOptions.LastName);
            Assert.Equal(result.FirstName, defaultOptions.FirstName);
            Assert.Equal(result.Age, defaultOptions.Age);
            Assert.Equal(result.Bool, defaultOptions.Bool);
            Assert.Equal(result.Location.Lat, defaultOptions.Location.Lat);
            Assert.Equal(result.Location.Long, defaultOptions.Location.Long);
        }

        [Fact]
        public void Typed_OnlyOverlay()
        {
            var defaultOptions = new Options() { };

            var overlay = new Options()
            {
                LastName = "Smith",
                FirstName = "Fred",
                Age = 22,
                Location = new Location() { Lat = 1.2312312F, Long = 3.234234F }
            };

            var result = ObjectPath.Merge(defaultOptions, overlay);
            Assert.Equal(result.LastName, overlay.LastName);
            Assert.Equal(result.FirstName, overlay.FirstName);
            Assert.Equal(result.Age, overlay.Age);
            Assert.Equal(result.Bool, overlay.Bool);
            Assert.Equal(result.Location.Lat, overlay.Location.Lat);
            Assert.Equal(result.Location.Long, overlay.Location.Long);
        }

        [Fact]
        public void Typed_FullOverlay()
        {
            var defaultOptions = new Options()
            {
                LastName = "Smith",
                FirstName = "Fred",
                Age = 22,
                Location = new Location() { Lat = 1.2312312F, Long = 3.234234F }
            };

            var overlay = new Options()
            {
                LastName = "Grant",
                FirstName = "Eddit",
                Age = 32,
                Bool = true,
                Location = new Location() { Lat = 2.2312312F, Long = 2.234234F }
            };

            var result = ObjectPath.Merge(defaultOptions, overlay);

            Assert.Equal(result.LastName, overlay.LastName);
            Assert.Equal(result.FirstName, overlay.FirstName);
            Assert.Equal(result.Age, overlay.Age);
            Assert.Equal(result.Bool, overlay.Bool);
            Assert.Equal(result.Location.Lat, overlay.Location.Lat);
            Assert.Equal(result.Location.Long, overlay.Location.Long);
        }

        [Fact]
        public void Typed_PartialOverlay()
        {
            var defaultOptions = new Options()
            {
                LastName = "Smith",
                FirstName = "Fred",
                Age = 22,
                Location = new Location() { Lat = 1.2312312F, Long = 3.234234F }
            };

            var overlay = new Options()
            {
                LastName = "Grant"
            };

            var result = ObjectPath.Merge(defaultOptions, overlay);

            Assert.Equal(result.LastName, overlay.LastName);
            Assert.Equal(result.FirstName, defaultOptions.FirstName);
            Assert.Equal(result.Age, defaultOptions.Age);
            Assert.Equal(result.Bool, defaultOptions.Bool);
            Assert.Equal(result.Location.Lat, defaultOptions.Location.Lat);
            Assert.Equal(result.Location.Long, defaultOptions.Location.Long);
        }

        [Fact]
        public void Anonymous_OnlyDefaultTest()
        {
            dynamic defaultOptions = new
            {
                LastName = "Smith",
                FirstName = "Fred",
                Age = 22,
                Bool = (bool?)true,
                Location = new { Lat = 1.2312312F, Long = 3.234234F }
            };
            dynamic overlay = new { };

            var result = ObjectPath.Assign<Options>(defaultOptions, overlay);
            Assert.Equal(result.LastName, defaultOptions.LastName);
            Assert.Equal(result.FirstName, defaultOptions.FirstName);
            Assert.Equal(result.Age, defaultOptions.Age);
            Assert.Equal(result.Bool, defaultOptions.Bool);
            Assert.Equal(result.Location.Lat, defaultOptions.Location.Lat);
            Assert.Equal(result.Location.Long, defaultOptions.Location.Long);
        }

        [Fact]
        public void Anonymous_OnlyOverlay()
        {
            dynamic defaultOptions = new { };

            dynamic overlay = new
            {
                LastName = "Smith",
                FirstName = "Fred",
                Age = 22,
                Bool = (bool?)true,
                Location = new { Lat = 1.2312312F, Long = 3.234234F }
            };

            var result = ObjectPath.Assign<Options>(defaultOptions, overlay);

            Assert.Equal(result.LastName, overlay.LastName);
            Assert.Equal(result.FirstName, overlay.FirstName);
            Assert.Equal(result.Age, overlay.Age);
            Assert.Equal(result.Bool, overlay.Bool);
            Assert.Equal(result.Location.Lat, overlay.Location.Lat);
            Assert.Equal(result.Location.Long, overlay.Location.Long);
        }

        [Fact]
        public void Anonymous_FullOverlay()
        {
            dynamic defaultOptions = new
            {
                LastName = "Smith",
                FirstName = "Fred",
                Age = 22,
                Bool = (bool?)true,
                Location = new { Lat = 1.2312312F, Long = 3.234234F }
            };

            dynamic overlay = new
            {
                LastName = "Grant",
                FirstName = "Eddit",
                Age = 32,
                Bool = (bool?)true,
                Location = new { Lat = 2.2312312F, Long = 2.234234F }
            };

            var result = ObjectPath.Assign<Options>(defaultOptions, overlay);

            Assert.Equal(result.LastName, overlay.LastName);
            Assert.Equal(result.FirstName, overlay.FirstName);
            Assert.Equal(result.Age, overlay.Age);
            Assert.Equal(result.Bool, overlay.Bool);
            Assert.Equal(result.Location.Lat, overlay.Location.Lat);
            Assert.Equal(result.Location.Long, overlay.Location.Long);
        }

        [Fact]
        public void Anonymous_PartialOverlay()
        {
            dynamic defaultOptions = new
            {
                LastName = "Smith",
                FirstName = "Fred",
                Age = 22,
                Bool = (bool?)true,
                Location = new { Lat = 1.2312312F, Long = 3.234234F }
            };

            dynamic overlay = new
            {
                LastName = "Grant"
            };

            var result = ObjectPath.Assign<Options>(defaultOptions, overlay);

            Assert.Equal(result.LastName, overlay.LastName);
            Assert.Equal(result.FirstName, defaultOptions.FirstName);
            Assert.Equal(result.Age, defaultOptions.Age);
            Assert.Equal(result.Bool, defaultOptions.Bool);
            Assert.Equal(result.Location.Lat, defaultOptions.Location.Lat);
            Assert.Equal(result.Location.Long, defaultOptions.Location.Long);
        }

        [Fact]
        public void JObject_OnlyDefaultTest()
        {
            dynamic defaultOptions = JsonSerializer.SerializeToNode(new Options()
            {
                LastName = "Smith",
                FirstName = "Fred",
                Age = 22,
                Location = new Location() { Lat = 1.2312312F, Long = 3.234234F }
            });
            
            dynamic overlay = JsonSerializer.SerializeToNode(new Options() { });

            var result = ObjectPath.Assign<Options>(defaultOptions, overlay);

            Assert.Equal(result.LastName, (string)defaultOptions["LastName"]);
            Assert.Equal(result.FirstName, (string)defaultOptions["FirstName"]);
            Assert.Equal(result.Age, (int?)defaultOptions["Age"]);
            Assert.Equal(result.Bool, (bool?)defaultOptions["Bool"]);
            Assert.Equal(result.Location.Lat, (float)defaultOptions["Location"]["Lat"]);
            Assert.Equal(result.Location.Long, (float)defaultOptions["Location"]["Long"]);
        }

        [Fact]
        public void JObject_OnlyOverlay()
        {
            dynamic defaultOptions = JsonSerializer.SerializeToNode(new Options() { });

            dynamic overlay = JsonSerializer.SerializeToNode(new Options()
            {
                LastName = "Smith",
                FirstName = "Fred",
                Age = 22,
                Location = new Location() { Lat = 1.2312312F, Long = 3.234234F }
            });

            var result = ObjectPath.Assign<Options>(defaultOptions, overlay);

            Assert.Equal(result.LastName, (string)overlay["LastName"]);
            Assert.Equal(result.FirstName, (string)overlay["FirstName"]);
            Assert.Equal(result.Age, (int?)overlay["Age"]);
            Assert.Equal(result.Bool, (bool?)overlay["Bool"]);
            Assert.Equal(result.Location.Lat, (float)overlay["Location"]["Lat"]);
            Assert.Equal(result.Location.Long, (float)overlay["Location"]["Long"]);
        }

        [Fact]
        public void JObject_FullOverlay()
        {
            dynamic defaultOptions = JsonSerializer.SerializeToNode(new Options()
            {
                LastName = "Smith",
                FirstName = "Fred",
                Age = 22,
                Location = new Location() { Lat = 1.2312312F, Long = 3.234234F }
            });

            dynamic overlay = JsonSerializer.SerializeToNode(new Options()
            {
                LastName = "Grant",
                FirstName = "Eddit",
                Age = 32,
                Bool = true,
                Location = new Location() { Lat = 2.2312312F, Long = 2.234234F }
            });

            var result = ObjectPath.Assign<Options>(defaultOptions, overlay);

            Assert.Equal(result.LastName, (string)overlay["LastName"]);
            Assert.Equal(result.FirstName, (string)overlay["FirstName"]);
            Assert.Equal(result.Age, (int?)overlay["Age"]);
            Assert.Equal(result.Bool, (bool?)overlay["Bool"]);
            Assert.Equal(result.Location.Lat, (float)overlay["Location"]["Lat"]);
            Assert.Equal(result.Location.Long, (float)overlay["Location"]["Long"]);
        }

        [Fact]
        public void JObject_PartialOverlay()
        {
            dynamic defaultOptions = JsonSerializer.SerializeToNode(new Options()
            {
                LastName = "Smith",
                FirstName = "Fred",
                Age = 22,
                Location = new Location() { Lat = 1.2312312F, Long = 3.234234F }
            });

            dynamic overlay = JsonSerializer.SerializeToNode(new Options()
            {
                LastName = "Grant"
            });

            var result = ObjectPath.Assign<Options>(defaultOptions, overlay);

            Assert.Equal(result.LastName, (string)overlay["LastName"]);
            Assert.Equal(result.FirstName, (string)defaultOptions["FirstName"]);
            Assert.Equal(result.Age, (int?)defaultOptions["Age"]);
            Assert.Equal(result.Bool, (bool?)defaultOptions["Bool"]);
            Assert.Equal(result.Location.Lat, (float)defaultOptions["Location"]["Lat"]);
            Assert.Equal(result.Location.Long, (float)defaultOptions["Location"]["Long"]);

        }

        [Fact]
        public void NullStartObject()
        {
            var defaultOptions = new Options()
            {
                LastName = "Smith",
                FirstName = "Fred",
                Age = 22,
                Location = new Location() { Lat = 1.2312312F, Long = 3.234234F }
            };

            var result = ObjectPath.Assign<Options>(null, defaultOptions);

            Assert.Equal(result.LastName, defaultOptions.LastName);
            Assert.Equal(result.FirstName, defaultOptions.FirstName);
            Assert.Equal(result.Age, defaultOptions.Age);
            Assert.Equal(result.Bool, defaultOptions.Bool);
            Assert.Equal(result.Location.Lat, defaultOptions.Location.Lat);
            Assert.Equal(result.Location.Long, defaultOptions.Location.Long);
        }

        [Fact]
        public void NullOverlay()
        {
            var defaultOptions = new Options()
            {
                LastName = "Smith",
                FirstName = "Fred",
                Age = 22,
                Location = new Location() { Lat = 1.2312312F, Long = 3.234234F }
            };

            var result = ObjectPath.Assign<Options>(defaultOptions, null);
            Assert.Equal(result.LastName, defaultOptions.LastName);
            Assert.Equal(result.FirstName, defaultOptions.FirstName);
            Assert.Equal(result.Age, defaultOptions.Age);
            Assert.Equal(result.Bool, defaultOptions.Bool);
            Assert.Equal(result.Location.Lat, defaultOptions.Location.Lat);
            Assert.Equal(result.Location.Long, defaultOptions.Location.Long);
        }

        [Fact]
        public void TryGetPathValue()
        {
            var test = new
            {
                test = "test",

                options = new
                {
                    Age = 15,
                    FirstName = "joe",
                    LastName = "blow",
                    Bool = false,
                },

                bar = new
                {
                    numIndex = 2,
                    strIndex = "FirstName",
                    objIndex = "options",
                    options = new Options()
                    {
                        Age = 1,
                        FirstName = "joe",
                        LastName = "blow",
                        Bool = false,
                    },
                    numbers = new int[] { 1, 2, 3, 4, 5 }
                },
            };

            // set with anonymous object
            {
                Assert.Equal(test, ObjectPath.GetPathValue<object>(test, string.Empty));
                Assert.Equal(test.test, ObjectPath.GetPathValue<string>(test, "test"));
                Assert.Equal(test.bar.options.Age, ObjectPath.GetPathValue<int>(test, "bar.options.age"));

                Assert.True(ObjectPath.TryGetPathValue<Options>(test, "options", out Options options));
                Assert.Equal(test.options.Age, options.Age);
                Assert.Equal(test.options.FirstName, options.FirstName);

                Assert.True(ObjectPath.TryGetPathValue<Options>(test, "bar.options", out Options barOptions));
                Assert.Equal(test.bar.options.Age, barOptions.Age);
                Assert.Equal(test.bar.options.FirstName, barOptions.FirstName);

                Assert.True(ObjectPath.TryGetPathValue<int[]>(test, "bar.numbers", out int[] numbers));
                Assert.Equal(5, numbers.Length);

                Assert.True(ObjectPath.TryGetPathValue<int>(test, "bar.numbers[1]", out int number));
                Assert.Equal(2, number);

                Assert.True(ObjectPath.TryGetPathValue<int>(test, "bar['options'].Age", out number));
                Assert.Equal(1, number);

                Assert.True(ObjectPath.TryGetPathValue<int>(test, "bar[\"options\"].Age", out number));
                Assert.Equal(1, number);

                Assert.True(ObjectPath.TryGetPathValue<int>(test, "bar.numbers[bar.numIndex]", out number));
                Assert.Equal(3, number);

                Assert.True(ObjectPath.TryGetPathValue<int>(test, "bar.numbers[bar[bar.objIndex].Age]", out number));
                Assert.Equal(2, number);

                Assert.True(ObjectPath.TryGetPathValue<string>(test, "bar.options[bar.strIndex]", out string name));
                Assert.Equal("joe", name);

                Assert.True(ObjectPath.TryGetPathValue<int>(test, "bar[bar.objIndex].Age", out int age));
                Assert.Equal(1, age);
            }

            // now try with JsonObject
            {
                var json = ProtocolJsonSerializer.ToJson(test);
                dynamic jtest = JsonSerializer.Deserialize<JsonNode>(json, ProtocolJsonSerializer.SerializationOptions);
                Assert.Equal(json, ProtocolJsonSerializer.ToJson(ObjectPath.GetPathValue<object>(jtest, string.Empty)));
                Assert.Equal((string)jtest["test"], ObjectPath.GetPathValue<string>(jtest, "test"));
                Assert.Equal((int)jtest["bar"]["options"]["Age"], ObjectPath.GetPathValue<int>(jtest, "bar.options.age"));

                Assert.True(ObjectPath.TryGetPathValue<Options>(jtest, "options", out Options options));
                Assert.Equal((int)jtest["options"]["Age"], options.Age);
                Assert.Equal((string)jtest["options"]["FirstName"], options.FirstName);

                Assert.True(ObjectPath.TryGetPathValue<Options>(jtest, "bar.options", out Options barOptions));
                Assert.Equal((int)jtest["bar"]["options"]["Age"], barOptions.Age);
                Assert.Equal((string)jtest["bar"]["options"]["FirstName"], barOptions.FirstName);

                Assert.True(ObjectPath.TryGetPathValue<int[]>(jtest, "bar.numbers", out int[] numbers));
                Assert.Equal(5, numbers.Length);

                Assert.True(ObjectPath.TryGetPathValue<int>(jtest, "bar.numbers[1]", out int number));
                Assert.Equal(2, number);

                Assert.True(ObjectPath.TryGetPathValue<int>(jtest, "bar['options'].Age", out number));
                Assert.Equal(1, number);

                Assert.True(ObjectPath.TryGetPathValue<int>(jtest, "bar[\"options\"].Age", out number));
                Assert.Equal(1, number);

                Assert.True(ObjectPath.TryGetPathValue<int>(jtest, "bar.numbers[bar.numIndex]", out int number2));
                Assert.Equal(3, number2);

                Assert.True(ObjectPath.TryGetPathValue<int>(jtest, "bar.numbers[bar[bar.objIndex].Age]", out int number3));
                Assert.Equal(2, number3);

                Assert.True(ObjectPath.TryGetPathValue<string>(jtest, "bar.options[bar.strIndex]", out string name));
                Assert.Equal("joe", name);

                Assert.True(ObjectPath.TryGetPathValue<int>(jtest, "bar[bar.objIndex].Age", out int age));
                Assert.Equal(1, age);

                jtest["bar"]["x.y.z"] = "test";

                Assert.True(ObjectPath.TryGetPathValue<string>(jtest, "bar['x.y.z']", out string split));
                Assert.Equal("test", split);

                Assert.True(ObjectPath.TryGetPathValue<string>(jtest, "bar[\"x.y.z\"]", out split));
                Assert.Equal("test", split);
            }
        }

        [Fact]
        public void SetPathValue()
        {
            const string dateISO = "2021-11-30T23:59:59:000Z";
            var test = new Dictionary<string, object>();

            ObjectPath.SetPathValue(test, "x.y.z", 15);
            ObjectPath.SetPathValue(test, "x.p", "hello");
            ObjectPath.SetPathValue(test, "foo", new { Bar = 15, Blat = "yo" });
            ObjectPath.SetPathValue(test, "x.a[1]", "yabba");
            ObjectPath.SetPathValue(test, "x.a[0]", "dabba");
            ObjectPath.SetPathValue(test, "null", null);
            ObjectPath.SetPathValue(test, "enum", TypeCode.Empty);
            ObjectPath.SetPathValue(test, "date.string.iso", dateISO);
            //ObjectPath.SetPathValue(test, "date.string.jtoken.iso", new JsonValue.Create(dateISO));
            ObjectPath.SetPathValue(test, "date.object", new { iso = dateISO });
            ObjectPath.SetPathValue(test, "date.object.jtoken", JsonSerializer.SerializeToElement(new { iso = dateISO }));

            Assert.Equal(15, ObjectPath.GetPathValue<int>(test, "x.y.z"));
            Assert.Equal("hello", ObjectPath.GetPathValue<string>(test, "x.p"));
            Assert.Equal(15, ObjectPath.GetPathValue<int>(test, "foo.bar"));
            Assert.Equal("yo", ObjectPath.GetPathValue<string>(test, "foo.Blat"));
            Assert.False(ObjectPath.TryGetPathValue<string>(test, "foo.Blatxxx", out var value));
            Assert.True(ObjectPath.TryGetPathValue<string>(test, "x.a[1]", out var value2));
            Assert.Equal("yabba", value2);
            Assert.True(ObjectPath.TryGetPathValue<string>(test, "x.a[0]", out value2));
            Assert.Equal("dabba", value2);
            Assert.False(ObjectPath.TryGetPathValue<object>(test, "null", out var nullValue));
            Assert.Equal(TypeCode.Empty, ObjectPath.GetPathValue<TypeCode>(test, "enum"));
            Assert.Equal(dateISO, ObjectPath.GetPathValue<string>(test, "date.string.iso"));
            //Assert.Equal(dateISO, ObjectPath.GetPathValue<string>(test, "date.string.jtoken.iso"));
            Assert.Equal(dateISO, ObjectPath.GetPathValue<string>(test, "date.object.iso"));
            Assert.Equal(dateISO, ObjectPath.GetPathValue<string>(test, "date.object.jtoken.iso"));

            // value type tests
#pragma warning disable SA1121 // Use built-in type alias
            AssertGetSetValueType(test, true);
            AssertGetSetValueType(test, DateTime.UtcNow);
            AssertGetSetValueType(test, DateTimeOffset.UtcNow);
            AssertGetSetValueType(test, Byte.MaxValue);
            AssertGetSetValueType(test, Int16.MaxValue);
            AssertGetSetValueType(test, Int32.MaxValue);
            AssertGetSetValueType(test, Int64.MaxValue);
            AssertGetSetValueType(test, UInt16.MaxValue);
            AssertGetSetValueType(test, UInt32.MaxValue);
            AssertGetSetValueType(test, UInt64.MaxValue);
            AssertGetSetValueType(test, Single.MaxValue);
            AssertGetSetValueType(test, Decimal.MaxValue);
            AssertGetSetValueType(test, Double.MaxValue);
#pragma warning restore SA1121 // Use built-in type alias
        }

        [Fact]
        public void RemovePathValue()
        {
            var test = new Dictionary<string, object>();
            ObjectPath.SetPathValue(test, "x.y.z", 15);
            ObjectPath.SetPathValue(test, "x.p", "hello");
            ObjectPath.SetPathValue(test, "foo", new { Bar = 15, Blat = "yo" });
            ObjectPath.SetPathValue(test, "x.a[1]", "yabba");
            ObjectPath.SetPathValue(test, "x.a[0]", "dabba");

            ObjectPath.RemovePathValue(test, "x.y.z");
            try
            {
                ObjectPath.GetPathValue<int>(test, "x.y.z");
                throw new XunitException("should have throw exception");
            }
            catch
            {
            }

            Assert.Null(ObjectPath.GetPathValue<string>(test, "x.y.z", null));
            Assert.Equal(99, ObjectPath.GetPathValue<int>(test, "x.y.z", 99));
            Assert.False(ObjectPath.TryGetPathValue<string>(test, "x.y.z", out var value));
            ObjectPath.RemovePathValue(test, "x.a[1]");
            Assert.False(ObjectPath.TryGetPathValue<string>(test, "x.a[1]", out string value2));
            Assert.True(ObjectPath.TryGetPathValue<string>(test, "x.a[0]", out value2));
            Assert.Equal("dabba", value2);
        }

        [Fact]
        public void Assign_BothNull()
        {
            // Bug fix: was (Type)Activator.CreateInstance(type) which threw InvalidCastException.
            var result = ObjectPath.Assign<Options>(null, null);
            Assert.NotNull(result);
            Assert.Null(result.FirstName);
            Assert.Null(result.LastName);
            Assert.Null(result.Age);
        }

        [Fact]
        public void GetPathValue_FloatJsonNumberToString()
        {
            // Bug fix: was GetValue<int>() which threw for non-integer numbers.
            var jobj = JsonNode.Parse("{\"value\": 3.14}");
            var result = ObjectPath.GetPathValue<string>(jobj, "value");
            Assert.Equal("3.14", result);
        }

        [Fact]
        public void GetPathValue_LargeIntJsonNumberToString()
        {
            // Verify Int64 numbers are not truncated by int conversion.
            var jobj = JsonNode.Parse($"{{\"value\": {long.MaxValue}}}");
            var result = ObjectPath.GetPathValue<string>(jobj, "value");
            Assert.Equal(long.MaxValue.ToString(), result);
        }

        [Fact]
        public void SetPathValue_IntermediateArrayExpansion()
        {
            // Bug fix: JsonArray intermediate nodes were not created; caused ArgumentOutOfRangeException.
            var test = new Dictionary<string, object>();

            ObjectPath.SetPathValue(test, "items[0].name", "Alice");
            ObjectPath.SetPathValue(test, "items[1].name", "Bob");

            Assert.Equal("Alice", ObjectPath.GetPathValue<string>(test, "items[0].name"));
            Assert.Equal("Bob", ObjectPath.GetPathValue<string>(test, "items[1].name"));
        }

        [Fact]
        public void GetProperties_NullReturnsEmpty()
        {
            // Bug fix: was empty block instead of yield break.
            var result = ObjectPath.GetProperties(null);
            Assert.Empty(result);
        }

        [Fact]
        public void GetProperties_Dictionary()
        {
            var dict = new Dictionary<string, object> { ["Alpha"] = 1, ["Beta"] = 2 };
            var props = ObjectPath.GetProperties(dict).ToList();
            Assert.Equal(2, props.Count);
            Assert.Contains("Alpha", props);
            Assert.Contains("Beta", props);
        }

        [Fact]
        public void GetProperties_JsonObject()
        {
            var jobj = JsonNode.Parse("{\"x\": 10, \"y\": 20}");
            var props = ObjectPath.GetProperties(jobj).ToList();
            Assert.Equal(2, props.Count);
            Assert.Contains("x", props);
            Assert.Contains("y", props);
        }

        [Fact]
        public void GetProperties_TypedObject()
        {
            var options = new Options { FirstName = "Fred" };
            var props = ObjectPath.GetProperties(options).ToList();
            Assert.Contains("FirstName", props);
            Assert.Contains("LastName", props);
            Assert.Contains("Age", props);
            Assert.Contains("Bool", props);
            Assert.Contains("Location", props);
        }

        [Fact]
        public void ContainsProperty_Null_ReturnsFalse()
        {
            Assert.False(ObjectPath.ContainsProperty(null, "anything"));
        }

        [Fact]
        public void ContainsProperty_Dictionary()
        {
            var dict = new Dictionary<string, object> { ["Name"] = "test" };
            Assert.True(ObjectPath.ContainsProperty(dict, "Name"));
            Assert.False(ObjectPath.ContainsProperty(dict, "Missing"));
        }

        [Fact]
        public void ContainsProperty_JsonObject()
        {
            var jobj = JsonNode.Parse("{\"Name\": \"test\"}");
            Assert.True(ObjectPath.ContainsProperty(jobj, "Name"));
            Assert.False(ObjectPath.ContainsProperty(jobj, "Missing"));
        }

        [Fact]
        public void ContainsProperty_TypedObject_CaseInsensitive()
        {
            var options = new Options { FirstName = "Fred" };
            Assert.True(ObjectPath.ContainsProperty(options, "FirstName"));
            Assert.True(ObjectPath.ContainsProperty(options, "firstname"));
            Assert.True(ObjectPath.ContainsProperty(options, "FIRSTNAME"));
            Assert.False(ObjectPath.ContainsProperty(options, "Missing"));
        }

        [Fact]
        public void ForEachProperty_Dictionary()
        {
            var dict = new Dictionary<string, object> { ["a"] = 1, ["b"] = "two" };
            var collected = new Dictionary<string, object>();
            ObjectPath.ForEachProperty(dict, (k, v) => collected[k] = v);
            Assert.Equal(2, collected.Count);
            Assert.Equal(1, collected["a"]);
            Assert.Equal("two", collected["b"]);
        }

        [Fact]
        public void ForEachProperty_JsonObject()
        {
            var jobj = JsonNode.Parse("{\"x\": 10, \"y\": 20}") as JsonObject;
            var keys = new List<string>();
            ObjectPath.ForEachProperty(jobj, (k, v) => keys.Add(k));
            Assert.Equal(2, keys.Count);
            Assert.Contains("x", keys);
            Assert.Contains("y", keys);
        }

        [Fact]
        public void HasValue_NullObject_ReturnsFalse()
        {
            Assert.False(ObjectPath.HasValue(null, "anything"));
        }

        [Fact]
        public void HasValue_NullPath_ReturnsFalse()
        {
            var obj = new Options { FirstName = "Fred" };
            Assert.False(ObjectPath.HasValue(obj, null));
        }

        // ── IDictionary<string, JsonElement> support ──────────────────────────────

        [Fact]
        public void GetPathValue_JsonElementDict_SimpleKey()
        {
            var json = JsonDocument.Parse("""{"name":"Alice","age":30}""");
            var dict = new Dictionary<string, JsonElement>
            {
                ["name"] = json.RootElement.GetProperty("name"),
                ["age"]  = json.RootElement.GetProperty("age"),
            };

            Assert.Equal("Alice", ObjectPath.GetPathValue<string>(dict, "name"));
            Assert.Equal(30, ObjectPath.GetPathValue<int>(dict, "age"));
        }

        [Fact]
        public void GetPathValue_JsonElementDict_CaseInsensitiveKey()
        {
            var json = JsonDocument.Parse("""{"TeamId":"T123"}""");
            var dict = new Dictionary<string, JsonElement>
            {
                ["TeamId"] = json.RootElement.GetProperty("TeamId"),
            };

            Assert.Equal("T123", ObjectPath.GetPathValue<string>(dict, "teamid"));
        }

        [Fact]
        public void GetPathValue_JsonElementDict_NestedObject()
        {
            var json = JsonDocument.Parse("""{"item":{"type":"message","ts":"123"}}""");
            var dict = new Dictionary<string, JsonElement>
            {
                ["item"] = json.RootElement.GetProperty("item"),
            };

            Assert.Equal("message", ObjectPath.GetPathValue<string>(dict, "item.type"));
            Assert.Equal("123",     ObjectPath.GetPathValue<string>(dict, "item.ts"));
        }

        [Fact]
        public void TryGetPathValue_JsonElementDict_MissingKey_ReturnsFalse()
        {
            var dict = new Dictionary<string, JsonElement>();

            Assert.False(ObjectPath.TryGetPathValue<string>(dict, "missing", out var value));
            Assert.Null(value);
        }

        [Fact]
        public void GetProperties_JsonElementDict()
        {
            var json = JsonDocument.Parse("""{"a":1,"b":2}""");
            var dict = new Dictionary<string, JsonElement>
            {
                ["a"] = json.RootElement.GetProperty("a"),
                ["b"] = json.RootElement.GetProperty("b"),
            };

            var keys = ObjectPath.GetProperties(dict).ToList();
            Assert.Contains("a", keys);
            Assert.Contains("b", keys);
        }

        [Fact]
        public void ContainsProperty_JsonElementDict()
        {
            var json = JsonDocument.Parse("""{"x":1}""");
            var dict = new Dictionary<string, JsonElement>
            {
                ["x"] = json.RootElement.GetProperty("x"),
            };

            Assert.True(ObjectPath.ContainsProperty(dict, "x"));
            Assert.False(ObjectPath.ContainsProperty(dict, "y"));
        }

        // ── Array out-of-range ────────────────────────────────────────────────

        [Fact]
        public void TryGetPathValue_JsonArray_OutOfRange_ReturnsFalse()
        {
            var jobj = JsonNode.Parse("""{"items":["a","b"]}""") as JsonObject;

            Assert.False(ObjectPath.TryGetPathValue<string>(jobj, "items[99]", out var value));
            Assert.Null(value);
        }

        [Fact]
        public void GetPathValue_JsonArray_OutOfRange_ReturnsDefault()
        {
            var jobj = JsonNode.Parse("""{"items":["a","b"]}""") as JsonObject;

            var result = ObjectPath.GetPathValue<string>(jobj, "items[99]", defaultValue: null);
            Assert.Null(result);
        }

        private void AssertGetSetValueType<T>(object test, T val)
        {
            ObjectPath.SetPathValue(test, val.GetType().Name, val);
            var result = ObjectPath.GetPathValue<T>(test, typeof(T).Name);
            Assert.Equal(val, result);
            Assert.Equal(val.GetType(), result.GetType());
        }
    }

    public class Location
    {
        public float? Lat { get; set; }

        public float? Long { get; set; }
    }

    public class Options
    {
        public string FirstName { get; set; }

        public string LastName { get; set; }

        public int? Age { get; set; }

        public bool? Bool { get; set; }

        public Location Location { get; set; }
    }
}
