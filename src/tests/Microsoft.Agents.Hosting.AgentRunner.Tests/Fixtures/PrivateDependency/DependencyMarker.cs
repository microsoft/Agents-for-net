// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace RunnerFixture
{
    public sealed class DependencyMarker
    {
        public string Message => "private dependency reached";
    }
}

public static class Program
{
    public static void RegisterServices(string wrongContract) { }
}
