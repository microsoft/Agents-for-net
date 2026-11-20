// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Core.Serialization;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Serialization;
using PlanStep = Microsoft.Teams.Apps.Schema.Entities.PlanStep;
using PlanType = Microsoft.Teams.Apps.Schema.Entities.PlanType;
using ErrorHelper = Microsoft.Agents.Extensions.MSTeams.Errors.ErrorHelper;

namespace Microsoft.Agents.Extensions.MSTeams.Models;

/// <summary>
/// Prompt-bound Plan progress carried by a Core activity.
/// </summary>
[Experimental("ExperimentalTeamsPlan")]
[EntityName(EntityName)]
public sealed class PlanInfoEntity : Entity
{
    public const string EntityName = "planInfo";

    private PlanType _planType = null!;

    public PlanInfoEntity() : base(EntityName)
    {
    }

    /// <summary>
    /// Gets or sets the Plan lifecycle operation.
    /// </summary>
    [JsonPropertyName("planType")]
    public required PlanType PlanType
    {
        get => _planType;
        set
        {
            if (value?.Value is not ("start" or "continue" or "complete" or "stop" or "error"))
            {
                throw Core.Errors.ExceptionHelper.GenerateException<JsonException>(ErrorHelper.PlanInvalidLifecycleOperation, null);
            }

            _planType = value;
        }
    }

    /// <summary>
    /// Gets or sets the supplied step revisions. Omitted steps remain unchanged.
    /// </summary>
    [JsonPropertyName("steps")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IList<PlanStep>? Steps { get; set; }
}
