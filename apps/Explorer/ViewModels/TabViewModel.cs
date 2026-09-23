using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Dispatching;
using EasyFileExplorer.Domain;
using EasyFileExplorer.FileSystem;
using EasyFileExplorer.Navigation;
using EasyFileExplorer.Operations;

namespace EasyFileExplorer.Explorer.ViewModels;

/// <summary>
/// Modelo de una pestaña: navegación con generaciones, vista de lista
/// virtualizable, orden, selección y búsqueda contextual.
/// </summary>
public sealed partial class TabViewModel : ObservableObject, IDisposable
{
    private readonly DispatcherQueue _dispatcher;
    private readonly IItemProvider _provider;
    private readonly LocalSearcher _searcher = new();
    private readonly ExplorerTab _tab;
    private readonly Dictionary<ItemId, ItemViewModel> _index = new();

    public TabViewModel(IItemProvider provider, Location initialLocation, DispatcherQueue dispatcher)
    {
        _provider = provider;
        _dispatcher = dispatcher;
        _tab = new ExplorerTab(provider, initialLocation);
        _tab.Session.ExternalChange += OnExternalChange;
        NavigateTo(initialLocation, recordHistory: true);
    }

    public ExplorerTab Tab => _tab;
    public ObservableCollection<ItemViewModel> Items { get; } = [];
    public ObservableCollection<LocationSegment> Breadcrumbs { get; } = [];

    [ObservableProperty]
    private string _title = string.Empty;

    [ObservableProperty]
    private string _address = string.Empty;

    [ObservableProperty]
    private string _statusText = string.Empty;

    [ObservableProperty]
    private bool _isEnumerating;

    [ObservableProperty]
    private bool _isSearchResults;

    [ObservableProperty]
    private string? _searchQuery;

    [ObservableProperty]
    private bool _canGoBack;

    [ObservableProperty]
    private bool _canGoForward;

    [ObservableProperty]
    private ItemViewModel? _focusedItem;

    [ObservableProperty]
    private bool _emptyVisible;

    public Location? CurrentLocation => _tab.Session.CurrentLocation;
    public IItemProvider Provider => _provider;

    // ---------- Navegación ----------

    public void NavigateTo(Location location, bool recordHistory = true)
    {
        if (recordHistory)
            _tab.History.Push(location);

        _ = EnumerateAsync(location);
    }

    /// <summary>Re-enumerar la ubicación actual (tras operaciones o cambios masivos).</summary>
    public void Refresh()
    {
        _tab.History.SaveSelection(_tab.Selection.Items.ToArray());
        var loc = CurrentLocation;
        if (loc is not null)
            _ = EnumerateAsync(loc);
    }

    [RelayCommand]
    private void GoBack()
    {
        var target = _tab.History.GoBack();
        if (target is not null)
            _ = EnumerateAsync(target);
    }

    [RelayCommand]
    private void GoForward()
    {
        var target = _tab.History.GoForward();
        if (target is not null)
            _ = EnumerateAsync(target);
    }

    [RelayCommand]
    private void GoUp()
    {
        var current = CurrentLocation;
        if (current is null)
            return;
        var parent = Path.GetDirectoryName(current.Address.TrimEnd(Path.DirectorySeparatorChar));
        if (string.IsNullOrEmpty(parent))
            return;
        NavigateTo(Location.LocalPath(parent));
    }

    [RelayCommand]
    private void GoHome()
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        NavigateTo(Location.LocalPath(home));
    }

    /// <summary>Abre un elemento: carpeta navega; archivo abre con el sistema.</summary>
    [RelayCommand]
    private void OpenItem(ItemViewModel? item)
    {
        if (item is null)
            return;
        if (item.IsFolder)
        {
            NavigateTo(Location.LocalPath(item.Path));
        }
        else
        {
            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(item.Path) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                StatusText = $"Cannot open '{item.Name}': {ex.Message}";
            }
        }
    }

    /// <summary>Navega a una dirección escrita por el usuario.</summary>
    [RelayCommand]
    private void NavigateToAddress(string address)
    {
        if (_provider.TryResolveLocation(address, out var location) && location is not null)
            NavigateTo(location);
        else
            StatusText = $"Cannot resolve '{address}'.";
    }

    // ---------- Enumeración ----------

    private async Task EnumerateAsync(Location location)
    {
        var request = _tab.Session.Navigate(location);
        var generation = request.Generation;

        Title = location.DisplayName;
        Address = location.Address;
        IsSearchResults = false;
        Breadcrumbs.Clear();
        foreach (var s in location.Segments)
            Breadcrumbs.Add(s);
        CanGoBack = _tab.History.CanGoBack;
        CanGoForward = _tab.History.CanGoForward;
        IsEnumerating = true;
        EmptyVisible = false;
        Items.Clear();
        _index.Clear();
        StatusText = "Loading...";

        var received = 0;
        try
        {
            await foreach (var batch in request.Batches)
            {
                // Descartar respuestas obsoletas de una generación anterior.
                if (!_tab.Session.IsCurrent(generation) || request.CancellationToken.IsCancellationRequested)
                    return;

                if (batch.Error is { } error)
                    StatusText = $"Partial result: {error.Message}";

                if (batch.Items.Count > 0)
                {
                    var sorted = SortItems(batch.Items);
                    if (_dispatcher.HasThreadAccess)
                    {
                        AddBatch(sorted);
                    }
                    else
                    {
                        await EnqueueUi(() => AddBatch(sorted));
                    }
                    received += batch.Items.Count;
                    UpdateStatus(received, done: false);
                }

                        if (batch.State == EnumerationState.Complete)
                {
                    await EnqueueUi(() =>
                    {
                        IsEnumerating = false;
                        EmptyVisible = Items.Count == 0;
                        UpdateStatus(received, done: true);
                    });
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Navegación cancelada por una nueva: normal, no es error.
        }
        catch (Exception ex)
        {
            await EnqueueUi(() =>
            {
                IsEnumerating = false;
                StatusText = $"Error: {ex.Message}";
            });
        }
    }

    private void AddBatch(IEnumerable<FileItem> items)
    {
        foreach (var item in items)
        {
            var vm = new ItemViewModel(item);
            _index[item.Id] = vm;
            Items.Add(vm);
        }
    }

    private Task EnqueueUi(Action action)
    {
        if (_dispatcher.HasThreadAccess)
        {
            action();
            return Task.CompletedTask;
        }
        var tcs = new TaskCompletionSource();
        _dispatcher.TryEnqueue(() =>
        {
            action();
            tcs.SetResult();
        });
        return tcs.Task;
    }

    // ---------- Orden ----------

    public void SetSort(SortColumn column)
    {
        var ascending = _tab.Sort.Column == column ? !_tab.Sort.Ascending : true;
        _tab.Sort = new SortState(column, ascending);

        var sorted = SortItems(Items.Select(v => v.Item)).ToArray();
        Items.Clear();
        _index.Clear();
        AddBatch(sorted);
    }

    private IEnumerable<FileItem> SortItems(IEnumerable<FileItem> items)
    {
        var comparer = Comparer<FileItem>.Create((a, b) =>
        {
            // Carpetas primero, como Explorer.
            if (a.IsFolder != b.IsFolder)
                return a.IsFolder ? -1 : 1;
            var c = _tab.Sort.Column switch
            {
                SortColumn.Size => Nullable.Compare(a.Size, b.Size),
                SortColumn.Modified => Nullable.Compare(a.ModifiedUtc, b.ModifiedUtc),
                SortColumn.Type => string.Compare(a.Extension, b.Extension, StringComparison.OrdinalIgnoreCase),
                _ => NaturalSort.Compare(a.Name, b.Name),
            };
            if (c == 0)
                c = NaturalSort.Compare(a.Name, b.Name);
            return _tab.Sort.Ascending ? c : -c;
        });
        return items.OrderBy(i => i, comparer);
    }

    private void UpdateStatus(int received, bool done)
    {
        var sel = _tab.Selection.Count;
        StatusText = done
            ? $"{Items.Count} items{(sel > 0 ? $" — {sel} selected" : "")}"
            : $"Loading... {received} items";
    }

    // ---------- Cambios externos ----------

    private void OnExternalChange(object? sender, FileChangeEvent ev)
    {
        _dispatcher.TryEnqueue(() =>
        {
            if (ev.Kind == FileChangeKind.Lost)
            {
                // Se perdieron eventos: reconciliar con enumeración nueva conservando selección.
                var loc = CurrentLocation;
                if (loc is not null)
                    _ = EnumerateAsync(loc);
                return;
            }

            switch (ev.Kind)
            {
                case FileChangeKind.Deleted when ev.Path is { } p:
                    RemoveByPath(p);
                    break;
                case FileChangeKind.Renamed when ev.OldPath is { } old && ev.Path is { } now:
                    RemoveByPath(old);
                    AddExternal(now);
                    break;
                case FileChangeKind.Created when ev.Path is { } p:
                    AddExternal(p);
                    break;
                case FileChangeKind.Modified when ev.Path is { } p:
                    RemoveByPath(p);
                    AddExternal(p);
                    break;
            }
        });
    }

    private void RemoveByPath(string path)
    {
        var toRemove = Items.Where(i => string.Equals(i.Path, path, StringComparison.OrdinalIgnoreCase)).ToArray();
        foreach (var vm in toRemove)
        {
            Items.Remove(vm);
            _index.Remove(vm.Id);
        }
        _tab.Selection.RetainExisting(_index.Keys);
        UpdateStatus(Items.Count, done: !IsEnumerating);
    }

    private void AddExternal(string path)
    {
        try
        {
            FileItem? item = null;
            if (File.Exists(path))
            {
                var fi = new FileInfo(path);
                item = new FileItem
                {
                    Id = ItemId.ForPath(WellKnownProviders.Local, fi.FullName),
                    Name = fi.Name,
                    IsFolder = false,
                    Size = fi.Length,
                    ModifiedUtc = fi.LastWriteTimeUtc,
                    CreatedUtc = fi.CreationTimeUtc,
                    Extension = fi.Extension,
                    Attributes = (uint)fi.Attributes,
                    IsHidden = (fi.Attributes & System.IO.FileAttributes.Hidden) != 0,
                    IsReadOnly = (fi.Attributes & System.IO.FileAttributes.ReadOnly) != 0,
                    IsReparsePoint = (fi.Attributes & System.IO.FileAttributes.ReparsePoint) != 0,
                };
            }
            else if (Directory.Exists(path))
            {
                var di = new DirectoryInfo(path);
                item = new FileItem
                {
                    Id = ItemId.ForPath(WellKnownProviders.Local, di.FullName),
                    Name = di.Name,
                    IsFolder = true,
                    ModifiedUtc = di.LastWriteTimeUtc,
                    CreatedUtc = di.CreationTimeUtc,
                    Attributes = (uint)di.Attributes,
                    IsHidden = (di.Attributes & System.IO.FileAttributes.Hidden) != 0,
                    IsReparsePoint = (di.Attributes & System.IO.FileAttributes.ReparsePoint) != 0,
                };
            }

            if (item is null || _index.ContainsKey(item.Id))
                return;

            var vm = new ItemViewModel(item);
            _index[item.Id] = vm;

            // Insertar en posición ordenada para no perturbar el orden visible.
            var pos = FindInsertPos(item);
            Items.Insert(pos, vm);
            UpdateStatus(Items.Count, done: !IsEnumerating);
        }
        catch { /* elemento desapareció entre evento y lectura */ }
    }

    private int FindInsertPos(FileItem item)
    {
        var comparer = Comparer<FileItem>.Create((a, b) =>
        {
            if (a.IsFolder != b.IsFolder)
                return a.IsFolder ? -1 : 1;
            var c = _tab.Sort.Column switch
            {
                SortColumn.Size => Nullable.Compare(a.Size, b.Size),
                SortColumn.Modified => Nullable.Compare(a.ModifiedUtc, b.ModifiedUtc),
                SortColumn.Type => string.Compare(a.Extension, b.Extension, StringComparison.OrdinalIgnoreCase),
                _ => NaturalSort.Compare(a.Name, b.Name),
            };
            if (c == 0)
                c = NaturalSort.Compare(a.Name, b.Name);
            return _tab.Sort.Ascending ? c : -c;
        });

        var lo = 0;
        var hi = Items.Count;
        while (lo < hi)
        {
            var mid = (lo + hi) / 2;
            if (comparer.Compare(Items[mid].Item, item) < 0)
                lo = mid + 1;
            else
                hi = mid;
        }
        return lo;
    }

    // ---------- Selección ----------

    public void SyncSelection(IList<object> selectedObjects)
    {
        _tab.Selection.Replace(selectedObjects.OfType<ItemViewModel>().Select(v => v.Id));
        UpdateStatus(Items.Count, done: !IsEnumerating);
    }

    // ---------- Búsqueda ----------

    public void Search(string text)
    {
        var scope = CurrentLocation;
        if (scope is null || string.IsNullOrWhiteSpace(text))
            return;

        SearchQuery = text;
        _ = SearchAsync(scope, text);
    }

    private async Task SearchAsync(Location scope, string text)
    {
        var request = _tab.Session.Navigate(scope); // nueva generación → cancela enumeración anterior
        var generation = request.Generation;

        IsSearchResults = true;
        IsEnumerating = true;
        Items.Clear();
        _index.Clear();
        StatusText = $"Searching '{text}'...";

        var query = new SearchQuery { Scope = scope, NameContains = text };
        try
        {
            await foreach (var batch in _searcher.SearchAsync(query, request.CancellationToken))
            {
                if (!_tab.Session.IsCurrent(generation) || request.CancellationToken.IsCancellationRequested)
                    return;

                if (batch.Items.Count > 0)
                {
                    var sorted = SortItems(batch.Items);
                    await EnqueueUi(() => AddBatch(sorted));
                }
                if (batch.State == EnumerationState.Complete)
                {
                    await EnqueueUi(() =>
                    {
                        IsEnumerating = false;
                        StatusText = $"{Items.Count} results for '{text}'";
                    });
                }
            }
        }
        catch (OperationCanceledException) { }
    }

    public void ExitSearch()
    {
        SearchQuery = null;
        var loc = CurrentLocation;
        if (loc is not null)
            _ = EnumerateAsync(loc);
    }

    public void Dispose()
    {
        _tab.Dispose();
    }
}
