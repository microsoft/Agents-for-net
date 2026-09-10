// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using A2A;

namespace Microsoft.Agents.Samples.A2AClient;

internal sealed class A2AConsole
{
    private static readonly JsonSerializerOptions s_agentCardJsonOptions = new()
    {
        WriteIndented = true,
    };

    private readonly IA2AClient _client;
    private readonly AgentCard _agentCard;
    private readonly A2AAuthenticationSession _authenticationSession;
    private readonly TextReader _input;
    private readonly TextWriter _output;
    private readonly bool _usePushNotifications;
    private readonly Uri _pushNotificationReceiver;
    private string? _taskId;
    private string? _contextId;
    private bool _useStreaming;

    public A2AConsole(
        IA2AClient client,
        AgentCard agentCard,
        A2AAuthenticationSession authenticationSession,
        TextReader input,
        TextWriter output,
        bool showHistory,
        bool usePushNotifications,
        Uri pushNotificationReceiver)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _agentCard = agentCard ?? throw new ArgumentNullException(nameof(agentCard));
        _authenticationSession = authenticationSession ?? throw new ArgumentNullException(nameof(authenticationSession));
        _input = input ?? throw new ArgumentNullException(nameof(input));
        _output = output ?? throw new ArgumentNullException(nameof(output));
        _usePushNotifications = usePushNotifications;
        _pushNotificationReceiver = pushNotificationReceiver ?? throw new ArgumentNullException(nameof(pushNotificationReceiver));
        ShowHistory = showHistory;
    }

    internal bool IsRunning { get; private set; } = true;

    internal bool ShowHistory { get; private set; }

    public async Task<int> RunAsync(CancellationToken cancellationToken)
    {
        _output.WriteLine("======= Agent Card ========");
        _output.WriteLine(JsonSerializer.Serialize(_agentCard, s_agentCardJsonOptions));

        if (_agentCard.Capabilities.Streaming == true)
        {
            _output.Write("Use streaming responses? (y/N): ");
            string? response = await _input.ReadLineAsync(cancellationToken).ConfigureAwait(false);
            _useStreaming = string.Equals(response?.Trim(), "y", StringComparison.OrdinalIgnoreCase)
                || string.Equals(response?.Trim(), "yes", StringComparison.OrdinalIgnoreCase);
        }

        while (IsRunning)
        {
            _output.Write("\nEnter a command or message (:q or quit to exit): ");
            string? prompt = await _input.ReadLineAsync(cancellationToken).ConfigureAwait(false);
            if (prompt is null)
            {
                break;
            }

            if (string.IsNullOrWhiteSpace(prompt))
            {
                _output.WriteLine("Request cannot be empty.");
                continue;
            }

            if (TryHandleCommand(prompt))
            {
                continue;
            }

            try
            {
                SendMessageRequest request = await CreateRequestAsync(prompt, cancellationToken).ConfigureAwait(false);
                AgentTask? task = _useStreaming
                    ? await SendStreamingAsync(request, cancellationToken).ConfigureAwait(false)
                    : await SendAsync(request, cancellationToken).ConfigureAwait(false);

                UpdateContinuation(task);

                if (ShowHistory && task is not null && !string.IsNullOrWhiteSpace(task.Id))
                {
                    AgentTask taskWithHistory = await _client.GetTaskAsync(
                        new GetTaskRequest { Id = task.Id, HistoryLength = 100 },
                        cancellationToken).ConfigureAwait(false);

                    _output.WriteLine("========= History =========");
                    A2AResponseWriter.WriteHistory(_output, taskWithHistory);
                }
            }
            catch (Exception exception) when (IsRecoverableRequestFailure(exception, cancellationToken))
            {
                // One failed send or history read must not end the session; the operator can retry,
                // switch authentication mode, or quit. Only the exception message is shown so no
                // request/response detail or credential can be written to the console.
                _output.WriteLine($"Request failed: {exception.GetType().Name}: {exception.Message}");
            }
        }

        return 0;
    }

    /// <summary>
    /// Identifies per-request failures the console can report and continue from. Cancellation requested
    /// through <paramref name="cancellationToken"/> is never treated as recoverable, so Ctrl+C still ends
    /// the loop.
    /// </summary>
    internal static bool IsRecoverableRequestFailure(Exception exception, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(exception);

        if (cancellationToken.IsCancellationRequested)
        {
            return false;
        }

        return exception is A2AException
            or HttpRequestException
            or HttpIOException
            or JsonException
            or InvalidOperationException
            or IOException
            or TimeoutException
            or OperationCanceledException;
    }

    internal bool TryHandleCommand(string input)
    {
        ArgumentNullException.ThrowIfNull(input);

        string command = input.Trim();
        if (string.Equals(command, ":q", StringComparison.OrdinalIgnoreCase)
            || string.Equals(command, "quit", StringComparison.OrdinalIgnoreCase))
        {
            IsRunning = false;
            return true;
        }

        if (command.StartsWith(":auth", StringComparison.OrdinalIgnoreCase))
        {
            string modeName = command[":auth".Length..].Trim();
            if (Enum.TryParse(modeName, ignoreCase: true, out A2AAuthMode mode)
                && Enum.IsDefined(mode))
            {
                bool modeChanged = mode != _authenticationSession.Mode;
                _authenticationSession.SetMode(mode);
                _output.WriteLine($"Authentication mode: {mode.ToString().ToLowerInvariant()}");

                if (modeChanged && _taskId is not null)
                {
                    // The pending task belongs to the previous credential. Continuing it under another
                    // principal would send that principal's input into the earlier caller's task.
                    ClearContinuation();
                    _output.WriteLine("Cleared the continuing task because the authentication mode changed.");
                }
                else if (modeChanged)
                {
                    ClearContinuation();
                }
            }
            else
            {
                _output.WriteLine("Usage: :auth none|delegated|app");
            }

            return true;
        }

        if (command.StartsWith(":history", StringComparison.OrdinalIgnoreCase))
        {
            string setting = command[":history".Length..].Trim();
            if (string.Equals(setting, "on", StringComparison.OrdinalIgnoreCase))
            {
                ShowHistory = true;
                _output.WriteLine("History display: on");
            }
            else if (string.Equals(setting, "off", StringComparison.OrdinalIgnoreCase))
            {
                ShowHistory = false;
                _output.WriteLine("History display: off");
            }
            else
            {
                _output.WriteLine("Usage: :history on|off");
            }

            return true;
        }

        if (command.StartsWith(':'))
        {
            _output.WriteLine($"Unknown command '{command}'.");
            return true;
        }

        return false;
    }

    private async Task<SendMessageRequest> CreateRequestAsync(string prompt, CancellationToken cancellationToken)
    {
        var message = new Message
        {
            Role = Role.User,
            MessageId = Guid.NewGuid().ToString("N"),
            ContextId = _contextId,
            TaskId = _taskId,
            Parts = [Part.FromText(prompt)],
        };

        _output.Write("Attachment path (press enter to skip): ");
        string? filePath = await _input.ReadLineAsync(cancellationToken).ConfigureAwait(false);
        if (!string.IsNullOrWhiteSpace(filePath))
        {
            try
            {
                byte[] content = await File.ReadAllBytesAsync(filePath, cancellationToken).ConfigureAwait(false);
                message.Parts.Add(Part.FromRaw(content, filename: Path.GetFileName(filePath)));
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                _output.WriteLine($"Unable to read attachment: {exception.Message}");
            }
        }

        var configuration = new SendMessageConfiguration
        {
            AcceptedOutputModes = ["text"],
        };

        if (_usePushNotifications)
        {
            configuration.PushNotificationConfig = new PushNotificationConfig
            {
                Url = GetNotificationUri(_pushNotificationReceiver).AbsoluteUri,
                Authentication = new AuthenticationInfo
                {
                    Scheme = "bearer",
                },
            };
        }

        return new SendMessageRequest
        {
            Message = message,
            Configuration = configuration,
        };
    }

    private async Task<AgentTask?> SendAsync(SendMessageRequest request, CancellationToken cancellationToken)
    {
        SendMessageResponse response = await _client.SendMessageAsync(request, cancellationToken).ConfigureAwait(false);
        A2AResponseWriter.Write(_output, response.Task);
        A2AResponseWriter.Write(_output, response.Message);
        return response.Task;
    }

    private async Task<AgentTask?> SendStreamingAsync(
        SendMessageRequest request,
        CancellationToken cancellationToken)
    {
        AgentTask? task = null;

        await foreach (StreamResponse response in _client
            .SendStreamingMessageAsync(request, cancellationToken)
            .WithCancellation(cancellationToken)
            .ConfigureAwait(false))
        {
            A2AResponseWriter.Write(_output, response);
            task = TaskProjection.Apply(task, response);
        }

        return task;
    }

    private void UpdateContinuation(AgentTask? task)
    {
        if (task?.Status.State == TaskState.InputRequired)
        {
            _taskId = task.Id;
            _contextId = task.ContextId;
            return;
        }

        ClearContinuation();
    }

    private void ClearContinuation()
    {
        _taskId = null;
        _contextId = null;
    }

    private static Uri GetNotificationUri(Uri receiver)
    {
        var builder = new UriBuilder(receiver)
        {
            Path = "/notify",
            Query = string.Empty,
            Fragment = string.Empty,
        };

        return builder.Uri;
    }
}
