// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;

namespace Microsoft.Agents.Storage.Tests
{
    public class MemoryStorageTests : StorageBaseTests, IDisposable
    {
        private IStorage storage;

        public MemoryStorageTests()
        {
            storage = new MemoryStorage();
        }

        public void Dispose()
        {
            storage = new MemoryStorage();
        }

        [Fact]
        public async Task MemoryStorage_ReadValidation()
        {
            await ReadValidation(storage);
        }

        [Fact]
        public async Task MemoryStorage_CreateObjectTest()
        {
            await CreateObjectTest(storage);
        }

        [Fact]
        public async Task MemoryStorage_ReadUnknownTest()
        {
            await ReadUnknownTest(storage);
        }

        [Fact]
        public async Task MemoryStorage_UpdateObjectTest()
        {
            await UpdateObjectTest<Exception>(storage);
        }

        [Fact]
        public async Task MemoryStorage_DeleteObjectTest()
        {
            await DeleteObjectTest(storage);
        }

        [Fact]
        public async Task MemoryStorage_HandleCrazyKeys()
        {
            await HandleCrazyKeys(storage);
        }

        [Fact]
        public async Task Nested()
        {
            var storage = new MemoryStorage();

            var outer = new Outer()
            {
                State = new Dictionary<string, object> 
                { 
                    ["key1"] = new Inner() { Name = "inner" } 
                }
            };

            var changes = new Dictionary<string, object>
                {
                    { "change1", outer.State },
                };

            await storage.WriteAsync(changes, default);
            var items = await storage.ReadAsync(new[] { "change1" }, default);

            Assert.NotEmpty(items);
        }
    }

    class Inner
    {
        public string Name { get; set; }
    }

    class Outer
    {
        public IDictionary<string, object> State { get; set; }
    }
}
