// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using A2A;

namespace Microsoft.Agents.Samples.A2AClient;

internal static class A2AResponseWriter
{
    public static string Format(AgentTask task)
    {
        ArgumentNullException.ThrowIfNull(task);

        return JoinText(
            GetText(task.Status.Message)
                .Concat(task.Artifacts?.SelectMany(artifact => GetText(artifact.Parts)) ?? []));
    }

    public static string Format(Message message)
    {
        ArgumentNullException.ThrowIfNull(message);
        return JoinText(GetText(message));
    }

    public static string Format(StreamResponse response)
    {
        ArgumentNullException.ThrowIfNull(response);

        return response.PayloadCase switch
        {
            StreamResponseCase.Task when response.Task is not null => Format(response.Task),
            StreamResponseCase.Message when response.Message is not null => Format(response.Message),
            StreamResponseCase.StatusUpdate when response.StatusUpdate is not null =>
                JoinText(GetText(response.StatusUpdate.Status.Message)),
            StreamResponseCase.ArtifactUpdate when response.ArtifactUpdate is not null =>
                JoinText(GetText(response.ArtifactUpdate.Artifact.Parts)),
            _ => string.Empty,
        };
    }

    public static string FormatHistory(AgentTask task)
    {
        ArgumentNullException.ThrowIfNull(task);
        return JoinText(task.History?.SelectMany(GetText) ?? []);
    }

    public static void Write(TextWriter writer, AgentTask? task)
    {
        ArgumentNullException.ThrowIfNull(writer);
        WriteText(writer, task is null ? string.Empty : Format(task));
    }

    public static void Write(TextWriter writer, Message? message)
    {
        ArgumentNullException.ThrowIfNull(writer);
        WriteText(writer, message is null ? string.Empty : Format(message));
    }

    public static void Write(TextWriter writer, StreamResponse response)
    {
        ArgumentNullException.ThrowIfNull(writer);
        WriteText(writer, Format(response));
    }

    public static void WriteHistory(TextWriter writer, AgentTask task)
    {
        ArgumentNullException.ThrowIfNull(writer);
        WriteText(writer, FormatHistory(task));
    }

    private static IEnumerable<string> GetText(Message? message)
        => message is null ? [] : GetText(message.Parts);

    private static IEnumerable<string> GetText(IEnumerable<Part> parts)
        => parts
            .Select(part => part.Text)
            .Where(text => !string.IsNullOrWhiteSpace(text))
            .Select(text => text!);

    private static string JoinText(IEnumerable<string> text)
        => string.Join(Environment.NewLine, text);

    private static void WriteText(TextWriter writer, string text)
    {
        if (!string.IsNullOrWhiteSpace(text))
        {
            writer.WriteLine(text);
        }
    }
}
