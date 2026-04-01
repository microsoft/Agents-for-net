using Microsoft.Agents.Core.Telemetry;
using System;
using System.Diagnostics;

#nullable enable

namespace Microsoft.Agents.Storage.Telemetry.Scopes
{
    internal class ScopeStorageOperation : TelemetryScope
    {
        private readonly string _operationName;
        private readonly int _keyCount;

        public ScopeStorageOperation(string scopeName, string operationName, int keyCount) : base(scopeName)
        {
            _operationName = operationName;
            _keyCount = keyCount;
        }

        protected override void Callback(System.Diagnostics.Activity telemetryActivity, double duration, Exception? exception)
        {
            telemetryActivity.SetTag(TagNames.KeyCount, _keyCount);
            telemetryActivity.SetTag(TagNames.StorageOperation, _operationName);

            TagList metricTags = new();
            metricTags.Add(TagNames.StorageOperation, _operationName);

            Metrics.OperationDuration.Record(1, metricTags);
            Metrics.OperationTotal.Add(1, metricTags);
        }
    }
}
