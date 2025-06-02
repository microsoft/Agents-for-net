// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace Microsoft.Agents.Hosting.A2A.Models
{
    public static class TaskState
    {
        public const string Submitted = "submitted";
        public const string Working = "working";
        public const string InputRequired = "input-required";
        public const string Completed = "completed";
        public const string Cancelled = "canceled";
        public const string Failed = "failed";
        public const string Rejected = "rejected";
        public const string AuthRequired = "auth-required";
        public const string Unknown = "unknown";
    }
}