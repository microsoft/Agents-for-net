#nullable enable

using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.ExceptionServices;
using System.Security;
using System.Text;
using Terminal.Gui.App;
using Terminal.Gui.Drivers;
using Terminal.Gui.Drawing;
using Terminal.Gui.Input;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;

internal sealed class TerminalChatState
{
    private readonly List<ChatEntry> _entries = [];
    private readonly Dictionary<string, int> _entryIndexes = new(StringComparer.Ordinal);
    private readonly Func<ChatEntry, bool> _includesEntry;
    private string? _activeActionGroupKey;

    internal TerminalChatState(Func<ChatEntry, bool>? includesEntry = null)
    {
        _includesEntry = includesEntry ?? (_ => true);
    }

    internal IReadOnlyList<ChatEntry> Entries => _entries;

    internal IReadOnlyList<ChatLink> Links =>
        GetActiveActionEntries()
            .SelectMany(entry => entry.Links)
            .ToArray();

    internal IReadOnlyList<ChatAction> SuggestedActions =>
        GetActiveActionEntries()
            .SelectMany(entry => entry.SuggestedActions)
            .ToArray();

    internal void Apply(IReadOnlyList<ChatChange> changes)
    {
        ArgumentNullException.ThrowIfNull(changes);

        string? newestActionGroupKey = null;
        foreach (ChatChange change in changes)
        {
            if (change.Kind == ChatChangeKind.Remove)
            {
                Remove(change.Key);
                continue;
            }

            if (change.Entry is null)
            {
                throw new ArgumentException("An upsert change must include an entry.", nameof(changes));
            }

            if (!_includesEntry(change.Entry))
            {
                Remove(change.Key);
                continue;
            }

            Upsert(change.Key, change.Entry);
            if (IsActionable(change.Entry))
            {
                newestActionGroupKey = change.Entry.ActionGroupKey;
            }
        }

        if (newestActionGroupKey is not null)
        {
            _activeActionGroupKey = newestActionGroupKey;
        }
        else if (_activeActionGroupKey is not null
            && !GetActiveActionEntries().Any(IsActionable))
        {
            _activeActionGroupKey = null;
        }
    }

    internal void ClearActions()
    {
        _activeActionGroupKey = null;
    }

    private void Upsert(string key, ChatEntry entry)
    {
        if (_entryIndexes.TryGetValue(key, out int existingIndex))
        {
            _entries[existingIndex] = entry;
            return;
        }

        _entryIndexes.Add(key, _entries.Count);
        _entries.Add(entry);
    }

    private void Remove(string key)
    {
        if (!_entryIndexes.Remove(key, out int removedIndex))
        {
            return;
        }

        _entries.RemoveAt(removedIndex);

        for (int index = removedIndex; index < _entries.Count; index++)
        {
            _entryIndexes[_entries[index].Key] = index;
        }
    }

    private IEnumerable<ChatEntry> GetActiveActionEntries()
    {
        return _activeActionGroupKey is null
            ? []
            : _entries.Where(
                entry => string.Equals(
                    entry.ActionGroupKey,
                    _activeActionGroupKey,
                    StringComparison.Ordinal));
    }

    private static bool IsActionable(ChatEntry entry)
    {
        return entry.Links.Count > 0 || entry.SuggestedActions.Count > 0;
    }
}

internal sealed class TerminalActivityState
{
    internal ObservableCollection<ActivityRecord> Records { get; } = [];

    internal ActivityRecord? Selected { get; private set; }

    internal string SelectedText => Selected?.Json ?? Selected?.Summary ?? string.Empty;

    internal void Add(ActivityRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);

        bool followsLatest = Selected is null
            || (Records.Count > 0 && ReferenceEquals(Selected, Records[^1]));

        Records.Add(record);
        if (followsLatest)
        {
            Selected = record;
        }
    }

    internal void Select(long? sequence)
    {
        Selected = sequence is null
            ? null
            : Records.FirstOrDefault(record => record.Sequence == sequence);
    }
}

internal sealed class TimelineRoleLabel : View
{
    private readonly TerminalPalette _palette;
    private string _content = string.Empty;
    private TimelineRole _role = TimelineRole.Primary;

    internal TimelineRoleLabel(TerminalPalette palette)
    {
        _palette = palette ?? throw new ArgumentNullException(nameof(palette));
        CanFocus = false;
        Height = 1;
    }

    internal string Content
    {
        get => _content;
        set
        {
            _content = value ?? string.Empty;
            SetNeedsDraw();
        }
    }

    internal TimelineRole Role
    {
        get => _role;
        set
        {
            _role = value;
            SetNeedsDraw();
        }
    }

    protected override bool OnDrawingContent(DrawContext? context)
    {
        int width = Math.Max(1, Viewport.Width > 0 ? Viewport.Width : Frame.Width);
        string text = Content.Length <= width ? Content : Content[..width];

        Move(0, 0);
        SetAttribute(_palette.Get(Role));
        AddStr(text.PadRight(width));
        return true;
    }
}

internal sealed class ChatTranscriptLayoutView : View
{
    private readonly View _header;
    private readonly View _transcript;
    private readonly View _actionBar;
    private readonly View _status;
    private readonly View _composer;
    private readonly View _footer;

    internal ChatTranscriptLayoutView(
        View header,
        View transcript,
        View actionBar,
        View status,
        View composer,
        View footer)
    {
        _header = header ?? throw new ArgumentNullException(nameof(header));
        _transcript = transcript ?? throw new ArgumentNullException(nameof(transcript));
        _actionBar = actionBar ?? throw new ArgumentNullException(nameof(actionBar));
        _status = status ?? throw new ArgumentNullException(nameof(status));
        _composer = composer ?? throw new ArgumentNullException(nameof(composer));
        _footer = footer ?? throw new ArgumentNullException(nameof(footer));
        CanFocus = true;

        Add(_header, _transcript, _actionBar, _status, _composer, _footer);
    }

    protected override void OnSubViewLayout(LayoutEventArgs args)
    {
        base.OnSubViewLayout(args);

        int width = Math.Max(0, Viewport.Width > 0 ? Viewport.Width : Frame.Width);
        int height = Math.Max(0, Viewport.Height > 0 ? Viewport.Height : Frame.Height);

        int transcriptHeight = height > 0 ? 1 : 0;
        int remainingHeight = Math.Max(0, height - transcriptHeight);
        int headerHeight = remainingHeight > 0 ? 1 : 0;
        remainingHeight -= headerHeight;

        int footerHeight = AllocateExactHeight(ref remainingHeight, 1);
        int composerHeight = AllocateExactHeight(ref remainingHeight, 3);
        int statusHeight = AllocateExactHeight(ref remainingHeight, 1);
        int actionBarHeight = AllocateHeight(ref remainingHeight, 2);
        transcriptHeight += remainingHeight;

        int y = 0;
        SetFrameAndLayout(_header, y, width, headerHeight);
        y += headerHeight;
        SetFrameAndLayout(_transcript, y, width, transcriptHeight);
        y += transcriptHeight;
        SetFrameAndLayout(_actionBar, y, width, actionBarHeight);
        y += actionBarHeight;
        SetFrameAndLayout(_status, y, width, statusHeight);
        y += statusHeight;
        SetFrameAndLayout(_composer, y, width, composerHeight);
        y += composerHeight;
        SetFrameAndLayout(_footer, y, width, footerHeight);
    }

    private static int AllocateHeight(ref int remainingHeight, int desiredHeight)
    {
        int allocatedHeight = Math.Min(remainingHeight, desiredHeight);
        remainingHeight -= allocatedHeight;
        return allocatedHeight;
    }

    private static int AllocateExactHeight(ref int remainingHeight, int desiredHeight)
    {
        if (remainingHeight < desiredHeight)
        {
            return 0;
        }

        remainingHeight -= desiredHeight;
        return desiredHeight;
    }

    private static void SetFrameAndLayout(View view, int y, int width, int height)
    {
        view.Frame = new System.Drawing.Rectangle(0, y, width, height);
        view.Layout();
    }
}

internal sealed class TerminalChatApplication : ITerminalView
{
    private const string HelpText =
        "F1      Chat\r\n"
        + "F2      Thoughts\r\n"
        + "F3      Activities\r\n"
        + "F4      Help\r\n"
        + "Esc     Return to chat\r\n"
        + "Ctrl+C  Copy focused link or selected activity JSON\r\n"
        + "Ctrl+Q  Quit\r\n"
        + "Enter   Send the composer text";
    private static readonly IReadOnlyList<TimelineLine> EmptyConversationLines = Array.AsReadOnly(
        new[]
        {
            new TimelineLine(string.Empty, [new TimelineSpan("Copilot Studio", TimelineRole.Primary)]),
            new TimelineLine(
                string.Empty,
                [new TimelineSpan("Connected conversations and streaming activity appear here.", TimelineRole.Muted)]),
            new TimelineLine(
                string.Empty,
                [new TimelineSpan("Type a message below to begin.", TimelineRole.Muted)])
        });

    private readonly TerminalOptions _options;
    private readonly Func<Uri, bool>? _confirmOpen;
    private readonly Action<ProcessStartInfo> _startProcess;
    private readonly Encoding _outputEncoding;
    private readonly TerminalChatState _chatState = new();
    private readonly TerminalChatState _thoughtState =
        new(entry => entry.Kind == ChatEntryKind.Thought);
    private readonly TerminalActivityState _activityState = new();
    private readonly List<ReceivedLink> _linkViews = [];
    private readonly HashSet<Task> _sendTasks = [];
    private readonly object _sendTasksGate = new();
    private IApplication? _application;
    private TerminalPresenter? _presenter;
    private CancellationTokenSource? _shutdownSource;
    private TerminalPalette? _palette;
    private Scheme? _controlScheme;
    private View? _chatTab;
    private View? _thoughtsTab;
    private View? _helpTab;
    private TerminalTimelineView? _transcript;
    private TerminalTimelineView? _thoughtTranscript;
    private TimelineRoleLabel? _status;
    private TextField? _composer;
    private ListView<ActivityRecord>? _activityList;
#pragma warning disable CS0618 // Task 7 explicitly requires TextView for the JSON inspector.
    private TextView? _json;
#pragma warning restore CS0618
    private View? _actionBar;
    private bool _isBusy;
    private bool _startupSucceeded;

    public TerminalChatApplication(TerminalOptions options)
        : this(
            options,
            confirmOpen: null,
            startProcess: startInfo => { Process.Start(startInfo); },
            outputEncoding: null)
    {
    }

    internal TerminalChatApplication(
        TerminalOptions options,
        Func<Uri, bool>? confirmOpen,
        Action<ProcessStartInfo> startProcess,
        Encoding? outputEncoding = null)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _confirmOpen = confirmOpen;
        _startProcess = startProcess ?? throw new ArgumentNullException(nameof(startProcess));
        _outputEncoding = outputEncoding ?? Console.OutputEncoding;
    }

    internal async Task RunAsync(
        TerminalPresenter presenter,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(presenter);

        using CancellationTokenSource shutdownSource =
            CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        using IApplication application = Application.Create();
        Runnable? shell = null;
        Exception? startupFailure = null;
        Task startupMonitor = Task.CompletedTask;
        _startupFailure = null;

        try
        {
            application.Init();
            shell = CreateShell(application, presenter, shutdownSource);

            using CancellationTokenRegistration cancellationRegistration =
                cancellationToken.Register(() => RequestStop(application));

            Task startupTask = Task.Run(
                () => presenter.StartAsync(shutdownSource.Token),
                CancellationToken.None);
            startupMonitor = MonitorStartupAsync(
                startupTask,
                action => application.Invoke(action),
                () => RequestStop(application),
                shutdownSource.Token);

            application.Run(shell);
        }
        finally
        {
            presenter.Dispose();
            shutdownSource.Cancel();
            await AwaitSendTasksAsync().ConfigureAwait(false);
            await startupMonitor.ConfigureAwait(false);
            startupFailure = _startupFailure;
            shell?.Dispose();
            _application = null;
            _presenter = null;
            _shutdownSource = null;
        }

        if (startupFailure is not null)
        {
            ExceptionDispatchInfo.Capture(startupFailure).Throw();
        }
    }

    private Exception? _startupFailure;

    internal Runnable CreateShell(
        IApplication application,
        TerminalPresenter presenter,
        CancellationTokenSource shutdownSource)
    {
        ArgumentNullException.ThrowIfNull(application);
        ArgumentNullException.ThrowIfNull(presenter);
        ArgumentNullException.ThrowIfNull(shutdownSource);

        _application = application;
        _presenter = presenter;
        _shutdownSource = shutdownSource;
        _startupSucceeded = false;
        IDriver driver = application.Driver
            ?? throw new InvalidOperationException("The terminal application has not been initialized.");
        _palette = TerminalPalette.Create(
            driver.DefaultAttribute,
            driver.SupportsTrueColor && !driver.Force16Colors);
        _controlScheme = _palette.CreateControlScheme();

        View chat = BuildChatTab();
        View thoughts = BuildThoughtsView();
        View activities = BuildActivitiesView();
        View help = BuildHelpView();
        _chatTab = chat;
        _thoughtsTab = thoughts;
        _helpTab = help;

        IReadOnlyDictionary<TerminalSurface, View> surfaces;

        if (_options.Layout == TerminalLayout.Tabs)
        {
            surfaces = new Dictionary<TerminalSurface, View>
            {
                [TerminalSurface.Chat] = chat,
                [TerminalSurface.Thoughts] = thoughts,
                [TerminalSurface.Activities] = activities,
                [TerminalSurface.Help] = help
            };
        }
        else
        {
            View left = new()
            {
                Title = "_Left",
                X = 0,
                Y = 0,
                Width = Dim.Percent(55),
                Height = Dim.Fill(),
                CanFocus = true,
                BorderStyle = LineStyle.None
            };
            left.SetScheme(GetControlScheme());
            left.Add(chat, thoughts);
            ShowSplitConversation(TerminalSurface.Chat);

            activities.X = Pos.Right(left);
            activities.Y = 0;
            activities.Width = Dim.Fill();
            activities.Height = Dim.Fill();

            View split = new()
            {
                Title = "_Split",
                X = 0,
                Y = 0,
                Width = Dim.Fill(),
                Height = Dim.Fill(),
                CanFocus = true,
                BorderStyle = LineStyle.None
            };
            split.SetScheme(GetControlScheme());
            split.Add(left, activities);

            surfaces = new Dictionary<TerminalSurface, View>
            {
                [TerminalSurface.Chat] = split,
                [TerminalSurface.Thoughts] = split,
                [TerminalSurface.Activities] = split,
                [TerminalSurface.Help] = help
            };
        }

        TerminalNavigationView navigation = new()
        {
            Palette = GetPalette()
        };
        TerminalShellView shell = new(
            navigation,
            surfaces,
            ResolveFocusTarget,
            CopySelection,
            RequestQuit,
            exception => SetStatus(
                $"Navigation failed: {exception.Message}",
                DiagnosticSeverity.Error));
        shell.SetScheme(GetControlScheme());
        shell.ContentRegion.SetScheme(GetControlScheme());
        shell.RegisterApplicationBindings(application);
        shell.Show(TerminalSurface.Chat);
        return shell;
    }

    public void AddActivity(ActivityRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);
        Invoke(() =>
        {
            _activityState.Add(record);
            if (_activityList is not null)
            {
                _activityList.Value = _activityState.Selected;
            }

            UpdateJsonView();
        });
    }

    public void ApplyChatChanges(IReadOnlyList<ChatChange> changes)
    {
        ArgumentNullException.ThrowIfNull(changes);
        Invoke(() =>
        {
            _chatState.Apply(changes);
            _thoughtState.Apply(changes);
            _transcript?.SetEntries(_chatState.Entries);
            _thoughtTranscript?.SetEntries(_thoughtState.Entries);

            RebuildActions();
        });
    }

    public void SetBusy(bool isBusy)
    {
        Invoke(() =>
        {
            _isBusy = isBusy;
            if (_composer is not null)
            {
                UpdateComposerEnabled();
            }
        });
    }

    public void SetStatus(string text, DiagnosticSeverity severity)
    {
        ArgumentNullException.ThrowIfNull(text);
        Invoke(() =>
        {
            if (_status is not null)
            {
                _status.Content = severity == DiagnosticSeverity.Information
                    ? text
                    : $"{severity}: {text}";
                _status.Role = severity switch
                {
                    DiagnosticSeverity.Error => TimelineRole.Error,
                    DiagnosticSeverity.Warning => TimelineRole.Warning,
                    _ => TimelineRole.Muted
                };
            }
        });
    }

    private View BuildChatTab()
    {
        View chat = new()
        {
            Title = "_Chat",
            Width = Dim.Fill(),
            Height = Dim.Fill(),
            CanFocus = true,
            BorderStyle = LineStyle.None
        };
        chat.SetScheme(GetControlScheme());

        View conversation = BuildChatView();
        chat.Add(conversation);
        return chat;
    }

    private View BuildChatView()
    {
        TimelineGlyphSet glyphs = TimelineGlyphSet.ForEncoding(_outputEncoding);
        TimelineRoleLabel header = new(GetPalette())
        {
            X = 0,
            Y = 0,
            Width = Dim.Fill(),
            Height = 1,
            Content = $"{glyphs.Agent} Copilot Studio  connected",
            Role = TimelineRole.Agent
        };
        header.SetScheme(GetControlScheme());

        _transcript = new TerminalTimelineView
        {
            X = 0,
            Y = 0,
            Width = Dim.Fill(),
            Height = Dim.Fill(),
            CollapseCompletedThoughts = true,
            Glyphs = glyphs,
            Palette = GetPalette(),
            EmptyStateLines = EmptyConversationLines
        };
        _transcript.SetScheme(GetControlScheme());
        _transcript.SetEntries(_chatState.Entries);

        _actionBar = new View
        {
            X = 0,
            Y = 0,
            Width = Dim.Fill(),
            Height = 2,
            CanFocus = false,
            BorderStyle = LineStyle.None
        };
        _actionBar.SetScheme(GetControlScheme());

        _status = new TimelineRoleLabel(GetPalette())
        {
            X = 0,
            Y = 0,
            Width = Dim.Fill(),
            Height = 1,
            Content = "Ready.",
            Role = TimelineRole.Muted
        };
        _status.SetScheme(GetControlScheme());

        _composer = new TextField
        {
            X = 0,
            Y = 0,
            Width = Dim.Fill(),
            Height = 1,
            Enabled = false
        };
        _composer.SetScheme(GetControlScheme());
        _composer.Accepting += (_, eventArgs) =>
        {
            eventArgs.Handled = true;
            SubmitComposer();
        };

        TerminalComposerView composer = new(
            _composer,
            GetPalette(),
            _outputEncoding.CodePage == Encoding.UTF8.CodePage)
        {
            BorderStyle = LineStyle.None
        };
        composer.SetScheme(GetControlScheme());

        TerminalFooterView footer = new(
            GetPalette(),
            _outputEncoding.CodePage == Encoding.UTF8.CodePage);
        footer.X = 0;
        footer.Y = 0;
        footer.Width = Dim.Fill();
        footer.Height = 1;
        footer.BorderStyle = LineStyle.None;
        footer.SetScheme(GetControlScheme());

        View conversation = new ChatTranscriptLayoutView(
            header,
            _transcript,
            _actionBar,
            _status,
            composer,
            footer)
        {
            Title = "_Conversation",
            Width = Dim.Fill(),
            Height = Dim.Fill(),
            CanFocus = true,
            BorderStyle = LineStyle.None
        };
        conversation.SetScheme(GetControlScheme());
        return conversation;
    }

    private View BuildActivitiesView()
    {
        View activities = new()
        {
            Title = "_Activities",
            Width = Dim.Fill(),
            Height = Dim.Fill(),
            CanFocus = true,
            BorderStyle = LineStyle.None
        };
        activities.SetScheme(GetControlScheme());

        _activityList = new ListView<ActivityRecord>
        {
        };
        _activityList.SetSource(_activityState.Records);
        _activityList.ValueChanged += (_, eventArgs) =>
        {
            _activityState.Select(eventArgs.NewValue?.Sequence);
            UpdateJsonView();
        };

#pragma warning disable CS0618 // Task 7 explicitly requires TextView for the JSON inspector.
        _json = new TextView
        {
            ReadOnly = true,
            ScrollBars = true,
            WordWrap = false,
            Text = string.Empty
        };
#pragma warning restore CS0618

        TerminalActivityView inspector = new(_activityList, _json, GetPalette());
        inspector.SetScheme(GetControlScheme());
        activities.Add(inspector);
        return activities;
    }

    private View BuildThoughtsView()
    {
        TimelineGlyphSet glyphs = TimelineGlyphSet.ForEncoding(_outputEncoding);
        View thoughts = new()
        {
            Title = "_Thoughts",
            Width = Dim.Fill(),
            Height = Dim.Fill(),
            CanFocus = true,
            BorderStyle = LineStyle.None
        };
        thoughts.SetScheme(GetControlScheme());

        _thoughtTranscript = new TerminalTimelineView
        {
            X = 0,
            Y = 0,
            Width = Dim.Fill(),
            Height = Dim.Fill(),
            CollapseCompletedThoughts = false,
            Glyphs = glyphs,
            Palette = GetPalette()
        };
        _thoughtTranscript.SetScheme(GetControlScheme());
        _thoughtTranscript.SetEntries(_thoughtState.Entries);

        thoughts.Add(_thoughtTranscript);
        return thoughts;
    }

    private View BuildHelpView()
    {
        View help = new()
        {
            Title = "_Help",
            Width = Dim.Fill(),
            Height = Dim.Fill(),
            CanFocus = true,
            BorderStyle = LineStyle.None
        };
        help.SetScheme(GetControlScheme());
        Label content = new()
        {
            X = 0,
            Y = 0,
            Width = Dim.Fill(),
            Height = Dim.Fill(),
            Text = HelpText
        };
        content.SetScheme(GetControlScheme());
        help.Add(content);
        return help;
    }

    private View? ResolveFocusTarget(TerminalSurface surface)
    {
        if (_options.Layout == TerminalLayout.Split
            && surface is TerminalSurface.Chat or TerminalSurface.Thoughts)
        {
            ShowSplitConversation(surface);
        }

        return surface switch
        {
            TerminalSurface.Chat when _composer?.Enabled == true => _composer,
            TerminalSurface.Chat => _transcript,
            TerminalSurface.Thoughts => _thoughtTranscript,
            TerminalSurface.Activities => _activityList,
            TerminalSurface.Help => _helpTab,
            _ => null
        };
    }

    private void ShowSplitConversation(TerminalSurface surface)
    {
        if (_chatTab is not null)
        {
            _chatTab.Visible = surface == TerminalSurface.Chat;
        }

        if (_thoughtsTab is not null)
        {
            _thoughtsTab.Visible = surface == TerminalSurface.Thoughts;
        }
    }

    private TerminalPalette GetPalette()
    {
        return _palette
            ?? throw new InvalidOperationException("The terminal palette has not been initialized.");
    }

    private Scheme GetControlScheme()
    {
        return _controlScheme
            ?? throw new InvalidOperationException("The terminal control scheme has not been initialized.");
    }

    internal void SubmitComposer()
    {
        if (_composer is null
            || _presenter is null
            || _shutdownSource is null
            || !_startupSucceeded
            || _isBusy)
        {
            return;
        }

        string text = _composer.Value ?? string.Empty;
        _composer.Value = string.Empty;
        _chatState.ClearActions();
        RebuildActions();
        TrackSend(ObserveSendAsync(_presenter.SendAsync(text, _shutdownSource.Token)));
    }

    private async Task ObserveSendAsync(Task sendTask)
    {
        try
        {
            await sendTask.ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (_shutdownSource?.IsCancellationRequested == true)
        {
        }
        catch (Exception exception)
        {
            SetStatus(
                string.IsNullOrWhiteSpace(exception.Message) ? exception.GetType().Name : exception.Message,
                DiagnosticSeverity.Error);
        }
    }

    private void TrackSend(Task sendTask)
    {
        lock (_sendTasksGate)
        {
            _sendTasks.Add(sendTask);
        }

        _ = sendTask.ContinueWith(
            completedTask =>
            {
                lock (_sendTasksGate)
                {
                    _sendTasks.Remove(completedTask);
                }
            },
            CancellationToken.None,
            TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);
    }

    private async Task AwaitSendTasksAsync()
    {
        Task[] pending;
        lock (_sendTasksGate)
        {
            pending = _sendTasks.ToArray();
        }

        await Task.WhenAll(pending).ConfigureAwait(false);
    }

    internal async Task MonitorStartupAsync(
        Task startupTask,
        Action<Action> invoke,
        Action requestStop,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(startupTask);
        ArgumentNullException.ThrowIfNull(invoke);
        ArgumentNullException.ThrowIfNull(requestStop);

        try
        {
            await startupTask.ConfigureAwait(false);
            if (!cancellationToken.IsCancellationRequested)
            {
                invoke(MarkStartupSucceeded);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            _startupFailure = exception;
            requestStop();
        }
    }

    private static void RequestStop(IApplication application)
    {
        try
        {
            application.Invoke(static app => app.RequestStop());
        }
        catch (ObjectDisposedException)
        {
            // A concurrent shutdown already restored and disposed the terminal.
        }
        catch (InvalidOperationException)
        {
            // The application is already stopping or has not finished initialization.
        }
    }

    private void RebuildActions()
    {
        if (_actionBar is null)
        {
            return;
        }

        _linkViews.Clear();
        foreach (View oldView in _actionBar.RemoveAll())
        {
            oldView.Dispose();
        }

        Pos linkX = 0;
        void AddLink(string text, Uri target)
        {
            ReceivedLink linkView = new(text, target, OpenReceivedLink)
            {
                X = linkX,
                Y = 0
            };
            linkView.SetScheme(GetControlScheme());
            _actionBar.Add(linkView);
            _linkViews.Add(linkView);
            linkX = Pos.Right(linkView) + 1;
        }

        foreach (ChatLink link in _chatState.Links)
        {
            AddLink(link.Title, link.Url);
        }

        HashSet<(string EntryKey, string Text, string Target)> markdownLinks = [];
        IEnumerable<TimelineLink> parsedLinks =
            (_transcript?.Links ?? [])
            .Concat(_thoughtTranscript?.Links ?? []);
        foreach (TimelineLink link in parsedLinks)
        {
            if (markdownLinks.Add((link.EntryKey, link.Text, link.Target.AbsoluteUri)))
            {
                AddLink(link.Text, link.Target);
            }
        }

        Pos actionX = 0;
        foreach (ChatAction action in _chatState.SuggestedActions)
        {
            Button button = new()
            {
                X = actionX,
                Y = 1,
                Text = action.Title
            };
            button.SetScheme(GetControlScheme());
            button.Accepted += (_, _) => ActivateAction(action);
            _actionBar.Add(button);
            actionX = Pos.Right(button) + 1;
        }

        _actionBar.CanFocus = _actionBar.SubViews.Count > 0;
    }

    private void ActivateAction(ChatAction action)
    {
        if (_composer is null)
        {
            return;
        }

        (string text, bool send) = ResolveAction(action);
        _composer.Value = text;
        if (send)
        {
            SubmitComposer();
        }
        else
        {
            _composer.SetFocus();
        }
    }

    internal void OpenReceivedLink(Uri target)
    {
        ArgumentNullException.ThrowIfNull(target);

        if (!CanOpenLink(target))
        {
            SetStatus($"Blocked unsupported link scheme: {target.Scheme}", DiagnosticSeverity.Error);
            return;
        }

        bool confirmed = _confirmOpen?.Invoke(target) ?? ConfirmOpen(target);
        if (!confirmed)
        {
            return;
        }

        try
        {
            _startProcess(new ProcessStartInfo(target.AbsoluteUri)
            {
                UseShellExecute = true
            });
        }
        catch (Exception exception) when (
            exception is Win32Exception
            or InvalidOperationException
            or PlatformNotSupportedException
            or IOException
            or SecurityException)
        {
            SetStatus($"Unable to open link: {exception.Message}", DiagnosticSeverity.Error);
        }
    }

    private bool ConfirmOpen(Uri target)
    {
        IApplication application = GetApplication();
        return MessageBox.Query(
            application,
            "Open link?",
            $"Open {target.AbsoluteUri}?",
            "_Open",
            "_Cancel") == 0;
    }

    private void CopySelection()
    {
        string? value = _linkViews
            .FirstOrDefault(link => link.HasFocus)
            ?.Target.AbsoluteUri;
        if (string.IsNullOrEmpty(value))
        {
            value = _activityState.SelectedText;
        }

        if (string.IsNullOrEmpty(value))
        {
            SetStatus("Nothing is selected to copy.", DiagnosticSeverity.Information);
            return;
        }

        string copyValue = value;
        IApplication application = GetApplication();
        IClipboard? clipboard = application.Clipboard;
        if (clipboard?.IsSupported == true
            && clipboard.TrySetClipboardData(copyValue))
        {
            SetStatus("Copied to clipboard.", DiagnosticSeverity.Information);
            return;
        }

        ShowSelectableValue(copyValue);
        SetStatus("Clipboard unavailable; the value is shown for manual copy.", DiagnosticSeverity.Information);
    }

    private void ShowSelectableValue(string value)
    {
        IApplication application = GetApplication();
        using Dialog dialog = new()
        {
            Title = "Copy value",
            Width = Dim.Percent(80),
            Height = Dim.Percent(60)
        };
        dialog.SetScheme(GetControlScheme());
#pragma warning disable CS0618 // Task 7 explicitly requires selectable TextView fallback content.
        TextView textView = new()
        {
            X = 0,
            Y = 0,
            Width = Dim.Fill(),
            Height = Dim.Fill(1),
            ReadOnly = true,
            ScrollBars = true,
            Text = value
        };
        textView.SetScheme(GetControlScheme());
#pragma warning restore CS0618
        Button close = new()
        {
            X = Pos.Center(),
            Y = Pos.AnchorEnd(1),
            IsDefault = true,
            Text = "_Close"
        };
        close.SetScheme(GetControlScheme());
        close.Accepted += (_, _) => application.RequestStop(dialog);
        dialog.Add(textView, close);
        application.Run(dialog);
    }

    private void RequestQuit()
    {
        _shutdownSource?.Cancel();
        _application?.RequestStop();
    }

    private void UpdateJsonView()
    {
        if (_json is not null)
        {
            _json.Text = _activityState.SelectedText;
        }
    }

    private void MarkStartupSucceeded()
    {
        if (_shutdownSource?.IsCancellationRequested == true)
        {
            return;
        }

        _startupSucceeded = true;
        UpdateComposerEnabled();
    }

    private void UpdateComposerEnabled()
    {
        if (_composer is not null)
        {
            _composer.Enabled = _startupSucceeded && !_isBusy;
        }
    }

    private void Invoke(Action action)
    {
        GetApplication().Invoke(action);
    }

    private IApplication GetApplication()
    {
        return _application
            ?? throw new InvalidOperationException("The terminal application has not been initialized.");
    }

    internal static bool CanOpenLink(Uri url)
    {
        ArgumentNullException.ThrowIfNull(url);
        return url.IsAbsoluteUri
            && (url.Scheme == Uri.UriSchemeHttp || url.Scheme == Uri.UriSchemeHttps);
    }

    internal static (string Text, bool Send) ResolveAction(ChatAction action)
    {
        ArgumentNullException.ThrowIfNull(action);
        return string.IsNullOrWhiteSpace(action.Value)
            ? (action.Title, false)
            : (action.Value, true);
    }
}
