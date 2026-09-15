#nullable enable

using System.Collections;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using Terminal.Gui.Text;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;

internal sealed class ActivityListDataSource : IListDataSource
{
    private readonly ObservableCollection<ActivityRecord> _records;
    private readonly Func<ActivityRecord, string> _formatter;
    private readonly List<string> _rows = [];
    private bool _disposed;
    private bool _hasPendingChange;
    private bool _suspendCollectionChangedEvent;

    internal ActivityListDataSource(
        ObservableCollection<ActivityRecord> records,
        Func<ActivityRecord, string>? formatter = null)
    {
        _records = records ?? throw new ArgumentNullException(nameof(records));
        _formatter = formatter ?? (record => record.ToString());
        RebuildRows();
        _records.CollectionChanged += OnRecordsChanged;
    }

    public event NotifyCollectionChangedEventHandler? CollectionChanged;

    public int Count => _records.Count;

    public int MaxItemLength { get; private set; }

    public bool SuspendCollectionChangedEvent
    {
        get => _suspendCollectionChangedEvent;
        set
        {
            if (_suspendCollectionChangedEvent == value)
            {
                return;
            }

            _suspendCollectionChangedEvent = value;
            if (!value && _hasPendingChange)
            {
                _hasPendingChange = false;
                CollectionChanged?.Invoke(
                    this,
                    new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
            }
        }
    }

    public bool IsMarked(int item) => false;

    public void SetMark(int item, bool value)
    {
    }

    public IList ToList() => _records;

    public bool RenderMark(
        ListView listView,
        int item,
        int row,
        bool isMarked,
        bool markMultiple) => false;

    public void Render(
        ListView listView,
        bool selected,
        int item,
        int col,
        int row,
        int width,
        int viewportX = 0)
    {
        ArgumentNullException.ThrowIfNull(listView);

        listView.Move(Math.Max(col - viewportX, 0), row);
        if (width <= 0)
        {
            return;
        }

        if (item < 0 || item >= _rows.Count)
        {
            listView.AddStr(new string(' ', width));
            return;
        }

        string text = _rows[item];
        if (string.IsNullOrEmpty(text) || viewportX >= text.GetColumns())
        {
            listView.AddStr(new string(' ', width));
            return;
        }

        int startIndex = Math.Min(viewportX, Math.Max(0, text.Length - 1));
        listView.AddStr(
            TextFormatter.ClipAndJustify(
                text[startIndex..],
                width,
                Alignment.Start));
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _records.CollectionChanged -= OnRecordsChanged;
        _disposed = true;
    }

    private void OnRecordsChanged(object? sender, NotifyCollectionChangedEventArgs eventArgs)
    {
        if (eventArgs.Action == NotifyCollectionChangedAction.Add
            && eventArgs.NewStartingIndex == _rows.Count
            && eventArgs.NewItems is not null)
        {
            foreach (ActivityRecord record in eventArgs.NewItems)
            {
                AddRow(record);
            }
        }
        else
        {
            RebuildRows();
        }

        if (SuspendCollectionChangedEvent)
        {
            _hasPendingChange = true;
            return;
        }

        CollectionChanged?.Invoke(this, eventArgs);
    }

    private void AddRow(ActivityRecord record)
    {
        string row = _formatter(record);
        _rows.Add(row);
        MaxItemLength = Math.Max(MaxItemLength, row.GetColumns());
    }

    private void RebuildRows()
    {
        _rows.Clear();
        MaxItemLength = 0;
        foreach (ActivityRecord record in _records)
        {
            AddRow(record);
        }
    }
}
