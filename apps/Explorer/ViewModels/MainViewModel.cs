using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Dispatching;
using EasyFileExplorer.Domain;
using EasyFileExplorer.FileSystem;
using EasyFileExplorer.Operations;

namespace EasyFileExplorer.Explorer.ViewModels;

/// <summary>Modelo de la ventana: pestañas, panel lateral y centro de operaciones.</summary>
public sealed partial class MainViewModel : ObservableObject
{
    private readonly DispatcherQueue _dispatcher;
    private readonly LocalFileSystemProvider _provider = new();

    /// <summary>Resolvedor de conflictos asignado por MainWindow tras tener XamlRoot.</summary>
    public Func<OperationConflict, Task<ConflictResolution>>? ConflictUI
    {
        get => Operations.ConflictUI;
        set => Operations.ConflictUI = value;
    }

    public MainViewModel(DispatcherQueue dispatcher)
    {
        _dispatcher = dispatcher;
        Operations = new OperationsViewModel(new OperationJournal(OperationJournal.DefaultPath), dispatcher);
        Clipboard = new Services.ClipboardService();
        Thumbnails = new Services.ThumbnailService(dispatcher);
        QuickAccess = FileSystem.KnownLocations.QuickAccess();
        Drives = FileSystem.KnownLocations.Drives();
    }

    public ObservableCollection<TabViewModel> Tabs { get; } = [];

    public OperationsViewModel Operations { get; }
    public Services.ClipboardService Clipboard { get; }
    public Services.ThumbnailService Thumbnails { get; }

    public IReadOnlyList<KnownLocations.SideEntry> QuickAccess { get; }
    public IReadOnlyList<KnownLocations.SideEntry> Drives { get; }

    [ObservableProperty]
    private TabViewModel? _selectedTab;

    /// <summary>Crea una pestaña nueva y la selecciona.</summary>
    public TabViewModel CreateTab()
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var tab = new TabViewModel(_provider, Location.LocalPath(home), _dispatcher);
        Tabs.Add(tab);
        SelectedTab = tab;
        return tab;
    }

    [RelayCommand]
    private void NewTab() => CreateTab();

    public void NavigateSideEntry(KnownLocations.SideEntry entry)
    {
        SelectedTab?.NavigateTo(entry.Location);
    }

    public void CloseTab(TabViewModel tab)
    {
        Tabs.Remove(tab);
        tab.Dispose();
        if (Tabs.Count == 0)
            NewTab();
    }
}
