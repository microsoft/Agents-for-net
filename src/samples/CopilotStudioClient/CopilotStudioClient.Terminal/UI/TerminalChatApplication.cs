#nullable enable

using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.ExceptionServices;
using System.Security;
using Terminal.Gui.App;
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
    internal string Content { get; init; } = string.Empty;

    internal TimelineRole Role { get; init; } = TimelineRole.Primary;

    internal TimelineRoleLabel()
    {
        CanFocus = false;
    }

    protected override bool OnDrawingContent(DrawContext? context)
    {
        int width = Math.Max(1, Viewport.Width > 0 ? Viewport.Width : Frame.Width);
        string text = Content.Length <= width ? Content : Content[..width];

        Move(0, 0);
        SetAttribute(GetAttributeForRole(GetVisualRole(Role)));
        AddStr(text.PadRight(width));
        return true;
    }

    private static VisualRole GetVisualRole(TimelineRole role) => role switch
    {
        TimelineRole.Primary => VisualRole.Normal,
        TimelineRole.Muted => VisualRole.Disabled,
        TimelineRole.User => VisualRole.HotNormal,
        TimelineRole.Agent => VisualRole.Active,
        TimelineRole.Thought => VisualRole.HotNormal,
        TimelineRole.Link => VisualRole.Focus,
        TimelineRole.ActiveNavigation => VisualRole.HotFocus,
        TimelineRole.Warning => VisualRole.HotActive,
        TimelineRole.Error => VisualRole.HotActive,
        TimelineRole.Code => VisualRole.Code,
        _ => VisualRole.Normal
    };
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
        "Ctrl+1  Chat\r\n"
        + "Ctrl+2  Thoughts\r\n"
        + "Ctrl+3  Activities\r\n"
        + "Ctrl+4  Help\r\n"
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
    private Tabs? _tabs;
    private View? _chatTab;
    private View? _thoughtsTab;
    private View? _activitiesTab;
    private View? _helpTab;
    private TerminalTimelineView? _transcript;
    private TerminalTimelineView? _thoughtTranscript;
    private Label? _status;
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
            startProcess: startInfo => { Process.Start(startInfo); })
    {
    }

    internal TerminalChatApplication(
        TerminalOptions options,
        Func<Uri, bool>? confirmOpen,
        Action<ProcessStartInfo> startProcess)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _confirmOpen = confirmOpen;
        _startProcess = startProcess ?? throw new ArgumentNullException(nameof(startProcess));
    }

    internal async Task RunAsync(
        TerminalPresenter presenter,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(presenter);

        using CancellationTokenSource shutdownSource =
            CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        using IApplication application = Application.Create();
        Window? window = null;
        Exception? startupFailure = null;
        Task startupMonitor = Task.CompletedTask;
        _startupFailure = null;

        try
        {
            application.Init();
            window = CreateWindow(application, presenter, shutdownSource);

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

            application.Run(window);
        }
        finally
        {
            presenter.Dispose();
            shutdownSource.Cancel();
            await AwaitSendTasksAsync().ConfigureAwait(false);
            await startupMonitor.ConfigureAwait(false);
            startupFailure = _startupFailure;
            window?.Dispose();
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

    internal Window CreateWindow(
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

        Window window = new()
        {
            Title = "Copilot Studio Terminal Client",
            Width = Dim.Fill(),
            Height = Dim.Fill()
        };

        View chat = BuildChatTab();
        View thoughts = BuildThoughtsView();
        View activities = BuildActivitiesView();
        _chatTab = chat;
        _thoughtsTab = thoughts;
        _activitiesTab = activities;

        if (_options.Layout == TerminalLayout.Tabs)
        {
            View help = BuildHelpView();
            _helpTab = help;
            View surfaces = new()
            {
                Width = Dim.Fill(),
                Height = Dim.Fill(),
                CanFocus = true
            };
            surfaces.Add(chat, thoughts, activities, help);
            ShowDefaultSurface(chat);
            window.Add(surfaces);
        }
        else
        {
            Tabs conversationTabs = new()
            {
                X = 0,
                Y = 0,
                Width = Dim.Percent(55),
                Height = Dim.Fill()
            };
            conversationTabs.Add(chat, thoughts);
            conversationTabs.Value = chat;
            _tabs = conversationTabs;

            activities.X = Pos.Right(conversationTabs);
            activities.Y = 0;
            activities.Width = Dim.Fill();
            activities.Height = Dim.Fill();
            window.Add(conversationTabs, activities);
        }
        window.KeyDown += OnWindowKeyDown;
        return window;
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
                _status.Text = severity == DiagnosticSeverity.Information
                    ? text
                    : $"{severity}: {text}";
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
            CanFocus = true
        };

        View conversation = BuildChatView();
        chat.Add(conversation);
        return chat;
    }

    private View BuildChatView()
    {
        TimelineGlyphSet glyphs = TimelineGlyphSet.ForEncoding(Console.OutputEncoding);
        TimelineRoleLabel header = new()
        {
            X = 0,
            Y = 0,
            Width = Dim.Fill(),
            Height = 1,
            Content = "Copilot Studio (connected)",
            Role = TimelineRole.Primary
        };

        _transcript = new TerminalTimelineView
        {
            X = 0,
            Y = 0,
            Width = Dim.Fill(),
            Height = Dim.Fill(),
            CollapseCompletedThoughts = true,
            Glyphs = glyphs,
            EmptyStateLines = EmptyConversationLines
        };
        _transcript.SetEntries(_chatState.Entries);

        _actionBar = new View
        {
            X = 0,
            Y = 0,
            Width = Dim.Fill(),
            Height = 2,
            CanFocus = false
        };

        _status = new Label
        {
            X = 0,
            Y = 0,
            Width = Dim.Fill(),
            Height = 1,
            Text = "Ready."
        };

        FrameView composerFrame = new()
        {
            X = 0,
            Y = 0,
            Width = Dim.Fill(),
            Height = 3,
            Title = "_Message"
        };

        composerFrame.Add(new Label
        {
            X = 0,
            Y = 0,
            Width = 1,
            Height = 1,
            Text = ">"
        });

        _composer = new TextField
        {
            X = 2,
            Y = 0,
            Width = Dim.Fill(2),
            Height = 1,
            Enabled = false
        };
        _composer.Accepting += (_, eventArgs) =>
        {
            eventArgs.Handled = true;
            SubmitComposer();
        };
        composerFrame.Add(_composer);

        StatusBar footer = BuildStatusBar();
        footer.X = 0;
        footer.Y = 0;
        footer.Width = Dim.Fill();
        footer.Height = 1;

        View conversation = new ChatTranscriptLayoutView(
            header,
            _transcript,
            _actionBar,
            _status,
            composerFrame,
            footer)
        {
            Title = "_Conversation",
            Width = Dim.Fill(),
            Height = Dim.Fill(),
            CanFocus = true
        };
        return conversation;
    }

    private View BuildActivitiesView()
    {
        View activities = new()
        {
            Title = "_Activities",
            Width = Dim.Fill(),
            Height = Dim.Fill(),
            CanFocus = true
        };

        _activityList = new ListView<ActivityRecord>
        {
            X = 0,
            Y = 0,
            Width = Dim.Percent(40),
            Height = Dim.Fill()
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
            X = Pos.Right(_activityList),
            Y = 0,
            Width = Dim.Fill(),
            Height = Dim.Fill(),
            ReadOnly = true,
            ScrollBars = true,
            WordWrap = false,
            Text = string.Empty
        };
#pragma warning restore CS0618

        activities.Add(_activityList, _json);
        return activities;
    }

    private View BuildThoughtsView()
    {
        TimelineGlyphSet glyphs = TimelineGlyphSet.ForEncoding(Console.OutputEncoding);
        View thoughts = new()
        {
            Title = "_Thoughts",
            Width = Dim.Fill(),
            Height = Dim.Fill(),
            CanFocus = true
        };

        _thoughtTranscript = new TerminalTimelineView
        {
            X = 0,
            Y = 0,
            Width = Dim.Fill(),
            Height = Dim.Fill(),
            CollapseCompletedThoughts = false,
            Glyphs = glyphs
        };
        _thoughtTranscript.SetEntries(_thoughtState.Entries);

        thoughts.Add(_thoughtTranscript);
        return thoughts;
    }

    private static View BuildHelpView()
    {
        View help = new()
        {
            Title = "_Help",
            Width = Dim.Fill(),
            Height = Dim.Fill(),
            CanFocus = true
        };
        help.Add(new Label
        {
            X = 0,
            Y = 0,
            Width = Dim.Fill(),
            Height = Dim.Fill(),
            Text = HelpText
        });
        return help;
    }

    private StatusBar BuildStatusBar()
    {
        return new StatusBar(
        [
            new Shortcut(Key.D1.WithCtrl, "Chat", FocusChat),
            new Shortcut(Key.D2.WithCtrl, "Thoughts", FocusThoughts),
            new Shortcut(Key.D3.WithCtrl, "Activities", FocusActivities),
            new Shortcut(Key.D4.WithCtrl, "Help", ShowHelp),
            new Shortcut(Key.C.WithCtrl, "Copy", CopySelection),
            new Shortcut(Key.Q.WithCtrl, "Quit", RequestQuit)
        ]);
    }

    private void OnWindowKeyDown(object? sender, Key key)
    {
        if (key.Equals(Key.D1.WithCtrl))
        {
            FocusChat();
        }
        else if (key.Equals(Key.D2.WithCtrl))
        {
            FocusThoughts();
        }
        else if (key.Equals(Key.D3.WithCtrl))
        {
            FocusActivities();
        }
        else if (key.Equals(Key.D4.WithCtrl))
        {
            ShowHelp();
        }
        else if (key.Equals(Key.C.WithCtrl))
        {
            CopySelection();
        }
        else if (key.Equals(Key.Q.WithCtrl))
        {
            RequestQuit();
        }
        else
        {
            return;
        }

        key.Handled = true;
    }

    private void FocusChat()
    {
        if (_chatTab is not null)
        {
            if (_options.Layout == TerminalLayout.Tabs)
            {
                ShowDefaultSurface(_chatTab);
                _chatTab.SetFocus();
            }
            else if (_tabs is not null)
            {
                _tabs.Value = _chatTab;
            }
        }

        if (_composer?.Enabled == true)
        {
            _composer.SetFocus();
        }
        else
        {
            _transcript?.SetFocus();
        }
    }

    private void FocusActivities()
    {
        if (_options.Layout == TerminalLayout.Tabs && _activitiesTab is not null)
        {
            ShowDefaultSurface(_activitiesTab);
            _activitiesTab.SetFocus();
        }

        _activityList?.SetFocus();
    }

    private void FocusThoughts()
    {
        if (_thoughtsTab is not null)
        {
            if (_options.Layout == TerminalLayout.Tabs)
            {
                ShowDefaultSurface(_thoughtsTab);
                _thoughtsTab.SetFocus();
            }
            else if (_tabs is not null)
            {
                _tabs.Value = _thoughtsTab;
            }

            _thoughtTranscript?.SetFocus();
        }
    }

    private void ShowHelp()
    {
        if (_options.Layout == TerminalLayout.Tabs && _helpTab is not null)
        {
            ShowDefaultSurface(_helpTab);
            _helpTab.SetFocus();
            return;
        }

        IApplication application = GetApplication();
        MessageBox.Query(application, "Help", HelpText, "_Close");
    }

    private void ShowDefaultSurface(View selectedSurface)
    {
        if (_chatTab is not null)
        {
            _chatTab.Visible = ReferenceEquals(_chatTab, selectedSurface);
        }

        if (_thoughtsTab is not null)
        {
            _thoughtsTab.Visible = ReferenceEquals(_thoughtsTab, selectedSurface);
        }

        if (_activitiesTab is not null)
        {
            _activitiesTab.Visible = ReferenceEquals(_activitiesTab, selectedSurface);
        }

        if (_helpTab is not null)
        {
            _helpTab.Visible = ReferenceEquals(_helpTab, selectedSurface);
        }
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
        foreach (ChatLink link in _chatState.Links)
        {
            ReceivedLink linkView = new(link.Title, link.Url, OpenReceivedLink)
            {
                X = linkX,
                Y = 0
            };
            _actionBar.Add(linkView);
            _linkViews.Add(linkView);
            linkX = Pos.Right(linkView) + 1;
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
            button.Accepted += (_, _) => ActivateAction(action);
            _actionBar.Add(button);
            actionX = Pos.Right(button) + 1;
        }
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
#pragma warning restore CS0618
        Button close = new()
        {
            X = Pos.Center(),
            Y = Pos.AnchorEnd(1),
            IsDefault = true,
            Text = "_Close"
        };
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
