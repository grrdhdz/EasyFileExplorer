using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Navigation;
using Windows.ApplicationModel.DataTransfer;
using Windows.ApplicationModel.DataTransfer.DragDrop;
using Windows.System;
using Windows.Storage;
using EasyFileExplorer.Domain;
using EasyFileExplorer.FileSystem;
using EasyFileExplorer.Navigation;
using EasyFileExplorer.Explorer.ViewModels;

namespace EasyFileExplorer.Explorer.Views;

/// <summary>Panel de explorador de una pestaña (toolbar + lateral + lista + estado).</summary>
public sealed partial class ExplorerPane : UserControl
{
    public ExplorerPane()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    public TabViewModel? ViewModel { get; private set; }
    public MainViewModel? Main { get; private set; }

    public void Bind(MainViewModel main, TabViewModel tab)
    {
        Main = main;
        ViewModel = tab;
        // Los x:Bind evaluaron con ViewModel=null al materializar la pestaña;
        // forzamos re-evaluación ahora que el modelo existe.
        Bindings.Update();
        PopulateSideNav();
    }

    private static bool IsDown(VirtualKey key) =>
        Microsoft.UI.Input.InputKeyboardSource
            .GetKeyStateForCurrentThread(key)
            .HasFlag(Microsoft.UI.Input.VirtualKeyStates.Down);

    private static bool CtrlDown => IsDown(VirtualKey.Control);
    private static bool ShiftDown => IsDown(VirtualKey.Shift);

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        // Atajos de ventana (Ctrl+L para la ruta, Espacio para vista rápida, etc.)
        // se gestionan en MainWindow.
    }

    private void PopulateSideNav()
    {
        if (Main is null)
            return;

        SideNav.Children.Add(SideHeader("Quick access"));
        foreach (var entry in Main.QuickAccess)
            SideNav.Children.Add(SideItem(entry));

        SideNav.Children.Add(SideHeader("This PC"));
        foreach (var entry in Main.Drives)
            SideNav.Children.Add(SideItem(entry));
    }

    private static TextBlock SideHeader(string text) => new()
    {
        Text = text,
        FontSize = 11,
        Margin = new Thickness(6, 10, 0, 2),
        Foreground = (Microsoft.UI.Xaml.Media.Brush)Microsoft.UI.Xaml.Application.Current.Resources["TextFillColorSecondaryBrush"],
    };

    private Button SideItem(KnownLocations.SideEntry entry)
    {
        var button = new Button
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Left,
            Background = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Transparent),
            Padding = new Thickness(6, 4, 6, 4),
            Content = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 8,
                Children =
                {
                    new FontIcon { Glyph = entry.Glyph, FontSize = 14 },
                    new TextBlock { Text = entry.Name, VerticalAlignment = VerticalAlignment.Center },
                },
            },
        };
        button.Click += (_, _) => ViewModel?.NavigateTo(entry.Location);
        return button;
    }

    // ---------- Navegación ----------

    private void OnBreadcrumbClicked(BreadcrumbBar sender, BreadcrumbBarItemClickedEventArgs args)
    {
        if (ViewModel is null || args.Item is not LocationSegment segment)
            return;
        ViewModel.NavigateTo(Location.LocalPath(segment.Address));
    }

    private void OnAddressKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == VirtualKey.Enter && ViewModel is not null)
        {
            ViewModel.NavigateToAddressCommand.Execute(AddressBox.Text);
            ShowBreadcrumbs();
            e.Handled = true;
        }
        else if (e.Key == VirtualKey.Escape)
        {
            ShowBreadcrumbs();
            e.Handled = true;
        }
    }

    private void OnAddressLostFocus(object sender, RoutedEventArgs e) => ShowBreadcrumbs();

    /// <summary>Ctrl+L: ruta editable.</summary>
    public void FocusAddress()
    {
        Breadcrumbs.Visibility = Visibility.Collapsed;
        AddressBox.Visibility = Visibility.Visible;
        AddressBox.Focus(FocusState.Programmatic);
        AddressBox.SelectAll();
    }

    public void FocusSearch() => SearchBox.Focus(FocusState.Programmatic);

    private void ShowBreadcrumbs()
    {
        AddressBox.Visibility = Visibility.Collapsed;
        Breadcrumbs.Visibility = Visibility.Visible;
    }

    // ---------- Búsqueda ----------

    private void OnSearchSubmitted(AutoSuggestBox sender, AutoSuggestBoxQuerySubmittedEventArgs args)
    {
        ViewModel?.Search(args.QueryText);
    }

    // ---------- Orden ----------

    private void OnSortName(object sender, RoutedEventArgs e) => ViewModel?.SetSort(SortColumn.Name);
    private void OnSortModified(object sender, RoutedEventArgs e) => ViewModel?.SetSort(SortColumn.Modified);
    private void OnSortType(object sender, RoutedEventArgs e) => ViewModel?.SetSort(SortColumn.Type);
    private void OnSortSize(object sender, RoutedEventArgs e) => ViewModel?.SetSort(SortColumn.Size);

    // ---------- Lista ----------

    private void OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        ViewModel?.SyncSelection(FileList.SelectedItems);
    }

    private void OnListDoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
    {
        if (FileList.SelectedItem is ItemViewModel item)
            ViewModel?.OpenItemCommand.Execute(item);
    }

    private void OnListKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (ViewModel is null)
            return;

        switch (e.Key)
        {
            case VirtualKey.Enter when FileList.SelectedItem is ItemViewModel item:
                ViewModel.OpenItemCommand.Execute(item);
                e.Handled = true;
                break;
            case VirtualKey.F2:
                _ = RenameSelectedAsync();
                e.Handled = true;
                break;
            case VirtualKey.Delete:
                _ = DeleteSelectedAsync(permanent: ShiftDown);
                e.Handled = true;
                break;
            case VirtualKey.C when CtrlDown:
                _ = CopySelectedAsync(cut: false);
                e.Handled = true;
                break;
            case VirtualKey.X when CtrlDown:
                _ = CopySelectedAsync(cut: true);
                e.Handled = true;
                break;
            case VirtualKey.V when CtrlDown:
                _ = PasteAsync();
                e.Handled = true;
                break;
            case VirtualKey.A when CtrlDown:
                FileList.SelectAll();
                e.Handled = true;
                break;
            case VirtualKey.Space:
                _ = QuickLookAsync();
                e.Handled = true;
                break;
        }
    }

    /// <summary>Vista rápida con Espacio: imagen o texto sin abrir otra app (MVP).</summary>
    private async Task QuickLookAsync()
    {
        if (FileList.SelectedItem is not ItemViewModel item || item.IsFolder || ViewModel is null)
            return;

        FrameworkElement content;
        var ext = item.Item.Extension;
        if (ext is ".txt" or ".md" or ".log" or ".json" or ".xml" or ".yaml" or ".yml" or ".cs" or ".csv")
        {
            var tb = new TextBlock
            {
                TextWrapping = TextWrapping.Wrap,
                FontFamily = new Microsoft.UI.Xaml.Media.FontFamily("Consolas"),
                IsTextSelectionEnabled = true,
            };
            try
            {
                const int max = 64 * 1024;
                await using var fs = new FileStream(item.Path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                var buf = new byte[Math.Min(max, fs.Length)];
                var n = await fs.ReadAsync(buf);
                tb.Text = System.Text.Encoding.UTF8.GetString(buf, 0, n);
            }
            catch (Exception ex)
            {
                tb.Text = $"Cannot preview: {ex.Message}";
            }
            content = new ScrollViewer { Content = tb, MaxHeight = 500 };
        }
        else if (ext is ".jpg" or ".jpeg" or ".png" or ".gif" or ".bmp" or ".webp")
        {
            try
            {
                var file = await StorageFile.GetFileFromPathAsync(item.Path);
                using var stream = await file.OpenReadAsync();
                var bmp = new Microsoft.UI.Xaml.Media.Imaging.BitmapImage();
                await bmp.SetSourceAsync(stream);
                content = new Image { Source = bmp, Stretch = Microsoft.UI.Xaml.Media.Stretch.Uniform, MaxWidth = 800, MaxHeight = 500 };
            }
            catch
            {
                content = new TextBlock { Text = "Cannot load image preview." };
            }
        }
        else
        {
            content = new StackPanel
            {
                Spacing = 4,
                Children =
                {
                    new TextBlock { Text = item.Name, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold },
                    new TextBlock { Text = item.TypeText },
                    new TextBlock { Text = item.SizeText },
                },
            };
        }

        var dialog = new ContentDialog
        {
            Title = item.Name,
            Content = content,
            CloseButtonText = "Close",
            XamlRoot = XamlRoot,
        };
        await dialog.ShowAsync();
    }

    /// <summary>Miniaturas solo para elementos visibles (ContainerContentChanging).</summary>
    private void OnContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
    {
        if (args.Item is ItemViewModel vm && ViewModel is not null && Main is not null)
        {
            var generation = ViewModel.Tab.Session.Generation;
            Main.Thumbnails.Request(vm, generation, g => ViewModel.Tab.Session.IsCurrent(g));
        }
    }

    // ---------- Drag & drop ----------

    private async void OnDragItemsStarting(object sender, DragItemsStartingEventArgs e)
    {
        if (Main is null || ViewModel is null)
            return;

        var items = e.Items.OfType<ItemViewModel>().Select(v => v.Id).ToArray();
        if (items.Length == 0)
            return;

        var storageItems = new List<IStorageItem>();
        foreach (var id in items)
        {
            try
            {
                if (Directory.Exists(id.Path))
                    storageItems.Add(await StorageFolder.GetFolderFromPathAsync(id.Path));
                else if (File.Exists(id.Path))
                    storageItems.Add(await StorageFile.GetFileFromPathAsync(id.Path));
            }
            catch { }
        }

        if (storageItems.Count > 0)
        {
            e.Data.SetStorageItems(storageItems);
            e.Data.RequestedOperation = DataPackageOperation.Copy | DataPackageOperation.Move;
        }
    }

    private void OnListDragOver(object sender, DragEventArgs e)
    {
        if (e.DataView.Contains(StandardDataFormats.StorageItems))
        {
            e.AcceptedOperation = e.Modifiers.HasFlag(Windows.ApplicationModel.DataTransfer.DragDrop.DragDropModifiers.Control)
                ? DataPackageOperation.Copy
                : DataPackageOperation.Move;
            e.DragUIOverride.Caption = e.AcceptedOperation == DataPackageOperation.Copy ? "Copy here" : "Move here";
            e.DragUIOverride.IsGlyphVisible = true;
            e.Handled = true;
        }
    }

    private async void OnListDrop(object sender, DragEventArgs e)
    {
        if (Main is null || ViewModel is null || !e.DataView.Contains(StandardDataFormats.StorageItems))
            return;

        var storageItems = await e.DataView.GetStorageItemsAsync();
        var sources = storageItems
            .Where(i => !string.IsNullOrEmpty(i.Path))
            .Select(i => ItemId.ForPath(WellKnownProviders.Local, i.Path))
            .ToArray();
        if (sources.Length == 0)
            return;

        // Soltar sobre una carpeta concreta o sobre el fondo de la lista.
        var targetDir = ViewModel.CurrentLocation;
        if (e.OriginalSource is FrameworkElement fe && fe.DataContext is ItemViewModel { IsFolder: true } folderVm)
            targetDir = Location.LocalPath(folderVm.Path);

        if (targetDir is null)
            return;

        var kind = e.AcceptedOperation == DataPackageOperation.Copy ? OperationKind.Copy : OperationKind.Move;
        await RunOperationAsync(new OperationRequest
        {
            OperationId = Guid.NewGuid(),
            Kind = kind,
            Sources = sources,
            TargetDirectory = ItemId.ForPath(WellKnownProviders.Local, targetDir.Address),
        });
    }

    // ---------- Menú contextual ----------

    private void OnItemMenuOpening(object sender, object e)
    {
        // Sincroniza el estado del menú con la selección real.
    }

    private void OnMenuOpen(object sender, RoutedEventArgs e)
    {
        if (FileList.SelectedItem is ItemViewModel item)
            ViewModel?.OpenItemCommand.Execute(item);
    }

    private void OnMenuOpenWith(object sender, RoutedEventArgs e)
    {
        if (FileList.SelectedItem is ItemViewModel { IsFolder: false } item)
            OpenWithDialog(item.Path);
    }

    private async void OnMenuCopy(object sender, RoutedEventArgs e) => await CopySelectedAsync(cut: false);
    private async void OnMenuCut(object sender, RoutedEventArgs e) => await CopySelectedAsync(cut: true);
    private async void OnMenuPaste(object sender, RoutedEventArgs e) => await PasteAsync();

    private void OnMenuCopyPath(object sender, RoutedEventArgs e)
    {
        var paths = FileList.SelectedItems.OfType<ItemViewModel>().Select(v => v.Path);
        var text = string.Join(Environment.NewLine, paths);
        if (text.Length == 0)
            return;
        var package = new DataPackage();
        package.SetText(text);
        Clipboard.SetContent(package);
    }

    private async void OnMenuRename(object sender, RoutedEventArgs e) => await RenameSelectedAsync();
    private async void OnMenuDelete(object sender, RoutedEventArgs e) => await DeleteSelectedAsync(permanent: false);

    private void OnMenuProperties(object sender, RoutedEventArgs e)
    {
        if (FileList.SelectedItem is ItemViewModel item)
            ShowProperties(item.Path);
    }

    // ---------- Operaciones ----------

    private async Task CopySelectedAsync(bool cut)
    {
        if (Main is null || ViewModel is null)
            return;
        var items = FileList.SelectedItems.OfType<ItemViewModel>().Select(v => v.Id).ToArray();
        if (items.Length > 0)
            await Main.Clipboard.CopyAsync(items, cut);
    }

    private async Task PasteAsync()
    {
        if (Main is null || ViewModel is null || ViewModel.CurrentLocation is not { } target)
            return;
        var files = await Main.Clipboard.GetFilesAsync();
        if (files.Count == 0)
            return;

        var kind = Main.Clipboard.IsCut ? OperationKind.Move : OperationKind.Copy;
        await RunOperationAsync(new OperationRequest
        {
            OperationId = Guid.NewGuid(),
            Kind = kind,
            Sources = files.Select(p => ItemId.ForPath(WellKnownProviders.Local, p)).ToArray(),
            TargetDirectory = ItemId.ForPath(WellKnownProviders.Local, target.Address),
        });
    }

    private async Task RenameSelectedAsync()
    {
        if (ViewModel is null || FileList.SelectedItem is not ItemViewModel item)
            return;

        var box = new TextBox { Text = item.Name, PlaceholderText = "New name" };
        var selectLen = item.Name.Length;
        if (!item.IsFolder)
        {
            var dot = item.Name.LastIndexOf('.');
            if (dot > 0)
                selectLen = dot;
        }
        box.Select(0, selectLen);

        var dialog = new ContentDialog
        {
            Title = "Rename",
            Content = box,
            PrimaryButtonText = "Rename",
            CloseButtonText = "Cancel",
            XamlRoot = XamlRoot,
        };
        if (await dialog.ShowAsync() != ContentDialogResult.Primary)
            return;

        var newName = box.Text.Trim();
        if (newName.Length == 0 || newName == item.Name)
            return;

        await RunOperationAsync(new OperationRequest
        {
            OperationId = Guid.NewGuid(),
            Kind = OperationKind.Rename,
            Sources = [item.Id],
            NewName = newName,
        });
    }

    private async Task DeleteSelectedAsync(bool permanent)
    {
        if (ViewModel is null)
            return;
        var items = FileList.SelectedItems.OfType<ItemViewModel>().ToArray();
        if (items.Length == 0)
            return;

        var target = permanent ? DeleteTarget.Permanent : DeleteTarget.RecycleBin;
        if (permanent)
        {
            var warn = new ContentDialog
            {
                Title = "Permanently delete",
                Content = $"Delete {items.Length} item(s) permanently? This cannot be undone.",
                PrimaryButtonText = "Delete",
                CloseButtonText = "Cancel",
                DefaultButton = ContentDialogButton.Close,
                XamlRoot = XamlRoot,
            };
            if (await warn.ShowAsync() != ContentDialogResult.Primary)
                return;
        }

        await RunOperationAsync(new OperationRequest
        {
            OperationId = Guid.NewGuid(),
            Kind = OperationKind.Delete,
            Sources = items.Select(v => v.Id).ToArray(),
            DeleteTarget = target,
        });
    }

    public async Task NewFolderAsync()
    {
        if (ViewModel?.CurrentLocation is not { } loc)
            return;
        var name = UniqueName(loc.Address, "New folder");
        await RunOperationAsync(new OperationRequest
        {
            OperationId = Guid.NewGuid(),
            Kind = OperationKind.CreateFolder,
            Sources = [ItemId.ForPath(WellKnownProviders.Local, Path.Combine(loc.Address, name))],
        });
    }

    private static string UniqueName(string dir, string baseName)
    {
        if (!Directory.Exists(Path.Combine(dir, baseName)))
            return baseName;
        for (var i = 2; i < 1000; i++)
        {
            var candidate = $"{baseName} ({i})";
            if (!Directory.Exists(Path.Combine(dir, candidate)))
                return candidate;
        }
        return $"{baseName} ({Guid.NewGuid():N})";
    }

    /// <summary>
    /// Ejecuta una operación en el centro de operaciones (fuera del hilo UI),
    /// con diálogo de conflictos. Al terminar, refresca la vista.
    /// </summary>
    public async Task RunOperationAsync(OperationRequest request)
    {
        if (Main is null || ViewModel is null)
            return;

        var result = await Main.Operations.RunAsync(request);

        // Refrescar la ubicación actual (o reconciliación vía watcher).
        ViewModel.Refresh();

        if (!result.FullySucceeded)
            ViewModel.StatusText = $"{result.Succeeded} done, {result.Failed} failed, {result.Skipped} skipped";
    }

    /// <summary>Diálogo de conflicto: omitir / reemplazar / conservar ambos / aplicar al resto.</summary>
    public async Task<ConflictResolution> ResolveConflictAsync(OperationConflict conflict)
    {
        var applyAll = new CheckBox { Content = "Do this for remaining conflicts" };
        var panel = new StackPanel { Spacing = 8 };
        panel.Children.Add(new TextBlock { Text = $"Target already exists:", TextWrapping = TextWrapping.Wrap });
        panel.Children.Add(new TextBlock { Text = conflict.TargetPath, TextWrapping = TextWrapping.Wrap, Foreground = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["TextFillColorSecondaryBrush"] });
        panel.Children.Add(applyAll);

        var tcs = new TaskCompletionSource<ConflictResolution>();
        var dialog = new ContentDialog
        {
            Title = "Conflict",
            Content = panel,
            PrimaryButtonText = "Replace",
            SecondaryButtonText = "Keep both",
            CloseButtonText = "Skip",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = XamlRoot,
        };

        var result = await dialog.ShowAsync();
        var policy = result switch
        {
            ContentDialogResult.Primary => ConflictPolicy.Replace,
            ContentDialogResult.Secondary => ConflictPolicy.KeepBoth,
            _ => ConflictPolicy.Skip,
        };
        return new ConflictResolution { Policy = policy, ApplyToRemaining = applyAll.IsChecked == true };
    }

    private void OnOperationsClicked(object sender, RoutedEventArgs e)
    {
        if (Main is null)
            return;

        var flyout = new Flyout();
        var panel = new StackPanel { Spacing = 4, MinWidth = 320 };
        if (Main.Operations.Active.Count == 0)
        {
            panel.Children.Add(new TextBlock { Text = "No operations running" });
        }
        else
        {
            foreach (var op in Main.Operations.Active)
            {
                var row = new StackPanel { Spacing = 2 };
                row.Children.Add(new TextBlock { Text = op.Description, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold });
                row.Children.Add(new TextBlock { Text = op.ResultText, Foreground = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["TextFillColorSecondaryBrush"] });
                panel.Children.Add(row);
            }
        }
        flyout.Content = panel;
        flyout.ShowAt(OperationsButton);
    }

    // ---------- Integración Shell ----------

    private static void OpenWithDialog(string path)
    {
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = "rundll32.exe",
                Arguments = $"shell32.dll,OpenAs_RunDLL \"{path}\"",
                UseShellExecute = true,
            });
        }
        catch { }
    }

    private static void ShowProperties(string path)
    {
        try
        {
            // Delega en el diálogo nativo de propiedades del sistema.
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = "explorer.exe",
                Arguments = $"/select,\"{path}\"",
                UseShellExecute = true,
            });
        }
        catch { }
    }
}
