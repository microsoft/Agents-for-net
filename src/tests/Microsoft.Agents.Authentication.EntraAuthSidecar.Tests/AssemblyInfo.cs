// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

// Several tests mutate the process-wide SIDECAR_URL environment variable. Disable test
// parallelization for this assembly so those tests cannot race with each other or with
// tests that resolve the sidecar base URL from the environment.
[assembly: Xunit.CollectionBehavior(DisableTestParallelization = true)]