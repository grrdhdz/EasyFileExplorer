using Microsoft.UI.Composition.SystemBackdrops;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Windows.System;
using EasyFileExplorer.Explorer.Views;
using EasyFileExplorer.Explorer.ViewModels;

namespace EasyFileExplorer.Explorer;

public sealed partial class MainWindow : Window
{
    public MainViewModel ViewModel { get; }

    public MainWindow()
    {
        ViewModel = new MainViewModel(DispatcherQueue);

        InitializeComponent();
        Title = "EasyFileExplorer";
        AppWindow.Resize(new Windows.Graphics.SizeInt32(1400, 900));
        try
        {
            AppWindow.SetIcon(Path.Combine(AppContext.BaseDirectory, "Assets", "app.ico"));
        }
        catch { /* icono opcional en desarrollo */ }

        // Botones min/max/close integrados en la fila de pestañas.
        ExtendsContentIntoTitleBar = true;
        SetTitleBar(TitleBarDragArea);

        if (MicaController.IsSupported())
            SystemBackdrop = new MicaBackdrop();
        else if (DesktopAcrylicController.IsSupported())
            SystemBackdrop = new DesktopAcrylicBackdrop();

        Content.KeyDown += OnRootKeyDown;

        var initial = ViewModel.CreateTab();
        BindPane(initial);
        }

    // WinUI TabView materializa paneles con retraso: enlazamos al cargar.
    private void OnAddTab(TabView sender, object args)
    {
        var tab = ViewModel.CreateTab();
        BindPane(tab);
    }

    private void BindPane(TabViewModel tab)
    {
        // El TabViewItem se crea cuando el TabView procesa la colección; esperamos al layout.
        DispatcherQueue.TryEnqueue(async () =>
        {
            await Task.Delay(50);
            AttachPane(tab);
        });
    }

    private void AttachPane(TabViewModel tab)
    {
        var container = Tabs.ContainerFromItem(tab) as TabViewItem;
        var pane = container?.Content as ExplorerPane;
        if (pane is null)
        {
            // Reintentar tras el siguiente ciclo de layout.
            DispatcherQueue.TryEnqueue(async () =>
            {
                await Task.Delay(50);
                AttachPane(tab);
            });
            return;
        }

        pane.Bind(ViewModel, tab);
        // El resolvedor de conflictos necesita XamlRoot: usar el del panel activo.
        ViewModel.ConflictUI = pane.ResolveConflictAsync;
    }

    private void OnTabCloseRequested(TabView sender, TabViewTabCloseRequestedEventArgs args)
    {
        if (args.Item is TabViewModel tab)
            ViewModel.CloseTab(tab);
    }

    private void OnRootKeyDown(object sender, KeyRoutedEventArgs e)
    {
        var ctrl = Microsoft.UI.Input.InputKeyboardSource
            .GetKeyStateForCurrentThread(VirtualKey.Control)
            .HasFlag(Microsoft.UI.Input.VirtualKeyStates.Down);
        if (!ctrl)
            return;

        var pane = CurrentPane();
        switch (e.Key)
        {
            case VirtualKey.L:
                pane?.FocusAddress();
                e.Handled = true;
                break;
            case VirtualKey.F:
                pane?.FocusSearch();
                e.Handled = true;
                break;
            case VirtualKey.T:
                OnAddTab(Tabs, new object());
                e.Handled = true;
                break;
            case VirtualKey.W:
                if (ViewModel.SelectedTab is { } tab)
                    ViewModel.CloseTab(tab);
                e.Handled = true;
                break;
            case VirtualKey.N:
                _ = pane?.NewFolderAsync();
                e.Handled = true;
                break;
        }
    }

    private ExplorerPane? CurrentPane()
    {
        if (ViewModel.SelectedTab is null)
            return null;
        return (Tabs.ContainerFromItem(ViewModel.SelectedTab) as TabViewItem)?.Content as ExplorerPane;
    }
}
