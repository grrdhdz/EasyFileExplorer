// Copyright (c) Files Community
// Licensed under the MIT License.

using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Animation;
using System.Collections.Specialized;
using Windows.Graphics;
using WinRT;

namespace Files.App.Views
{
	/// <summary>
	/// Shows a floating window with the file operations currently in progress.
	/// The window reuses the live <see cref="StatusCenterItem"/> models so it stays
	/// in sync with the Status Center without touching the operation engine.
	/// </summary>
	internal static class FileOperationProgressWindow
	{
		private const int WindowWidth = 480;
		private const int TitleBarHeight = 36;
		private const int ItemHeight = 148;
		private const int ExpandedItemHeight = 104;
		private const int MinWindowHeight = 240;
		private const int MaxWindowHeight = 720;

		private static readonly StatusCenterViewModel _viewModel = Ioc.Default.GetRequiredService<StatusCenterViewModel>();
		private static readonly IUserSettingsService _userSettings = Ioc.Default.GetRequiredService<IUserSettingsService>();
		private static WindowEx? _window;
		private static bool _initialized;
		private static bool _userDismissed;
		private static CancellationTokenSource? _pendingShow;

		internal static ObservableCollection<StatusCenterItem> Operations { get; } = [];

		public static void Initialize()
		{
			if (_initialized)
				return;
			_initialized = true;

			_viewModel.StatusCenterItems.CollectionChanged += OnStatusCenterItemsChanged;
			_userSettings.LayoutSettingsService.PropertyChanged += OnSettingsPropertyChanged;
			foreach (var item in _viewModel.StatusCenterItems)
				TrackIfActive(item);
		}

		private static void OnSettingsPropertyChanged(object? sender, PropertyChangedEventArgs e)
		{
			if (e.PropertyName is not nameof(ILayoutSettingsService.ShowFileOperationProgressWindow))
				return;

			if (_userSettings.LayoutSettingsService.ShowFileOperationProgressWindow)
				EnsureShown();
			else
			{
				CancelPendingShow();
				_window?.Close();
			}
		}

		private static bool TracksOperation(StatusCenterItem item)
			=> item.FileSystemOperationReturnResult is ReturnResult.InProgress
				&& item.Operation is FileOperationType.Copy
					or FileOperationType.Move
					or FileOperationType.Delete
					or FileOperationType.Recycle;

		private static void OnStatusCenterItemsChanged(object? sender, NotifyCollectionChangedEventArgs e)
		{
			if (e.NewItems is not null)
				foreach (StatusCenterItem item in e.NewItems)
					TrackIfActive(item);

			if (e.OldItems is not null)
				foreach (StatusCenterItem item in e.OldItems)
					Untrack(item);
		}

		// The operation is actually transferring once enumeration finished (conflict
		// dialogs are resolved during enumeration); the item name is a fallback for
		// operations that never report enumeration completion.
		private static bool TransferStarted(StatusCenterItem item)
			=> !item.IsDiscovering || !string.IsNullOrEmpty(item.CurrentProcessingItemName);

		private static void TrackIfActive(StatusCenterItem item)
		{
			if (!TracksOperation(item) || Operations.Contains(item))
				return;

			Operations.Add(item);
			item.PropertyChanged += OnItemPropertyChanged;

			// A new operation always reopens the window
			_userDismissed = false;
			EnsureShown();
		}

		private static void OnItemPropertyChanged(object? sender, PropertyChangedEventArgs e)
		{
			if (sender is not StatusCenterItem item)
				return;

			if (e.PropertyName is nameof(StatusCenterItem.IsExpanded))
			{
				UpdateWindowSize();
				return;
			}

			// IsDiscovering doesn't raise changes; Message and the current item name
			// are updated when enumeration completes and transfer begins
			if (e.PropertyName is nameof(StatusCenterItem.Message)
				or nameof(StatusCenterItem.CurrentProcessingItemName))
			{
				EnsureShown();
			}
		}

		private static bool CanShow()
			=> Operations.Count > 0
				&& !_userDismissed
				&& _userSettings.LayoutSettingsService.ShowFileOperationProgressWindow
				&& Operations.Any(TransferStarted);

		private static void EnsureShown()
		{
			if (!CanShow())
				return;

			if (_window is not null && _window.AppWindow.IsVisible)
			{
				UpdateWindowSize();
				return;
			}

			// Debounce so sub-second operations never flash the window open
			var cts = _pendingShow ??= new();
			var token = cts.Token;
			_ = Task.Delay(400).ContinueWith(_ =>
			{
				if (token.IsCancellationRequested)
					return;

				MainWindow.Instance.DispatcherQueue.TryEnqueue(() =>
				{
					if (CanShow() && (_window is null || !_window.AppWindow.IsVisible))
						Show();
				});
			}, CancellationToken.None);
		}

		private static void CancelPendingShow()
		{
			_pendingShow?.Cancel();
			_pendingShow?.Dispose();
			_pendingShow = null;
		}

		private static void Untrack(StatusCenterItem item)
		{
			if (!Operations.Remove(item))
				return;

			item.PropertyChanged -= OnItemPropertyChanged;

			// Auto-close once nothing is in flight; closing the window never cancels operations
			if (Operations.Count == 0)
			{
				CancelPendingShow();
				_window?.Close();
			}
			else
			{
				UpdateWindowSize();
			}
		}

		private static void Show()
		{
			var themeService = Ioc.Default.GetRequiredService<IAppThemeModeService>();
			var window = _window ??= CreateWindow(themeService);
			if (window.Content is Frame frame)
			{
				frame.RequestedTheme = themeService.AppThemeMode;
				if (frame.Content is FileOperationProgressPage page)
					window.SetTitleBar(page.TitleBarElement);
			}
			themeService.SetAppThemeMode(window, window.AppWindow.TitleBar, themeService.AppThemeMode, callThemeModeChangedEvent: false);

			window.AppWindow.Show();
			window.Activate();
		}

		private static WindowEx CreateWindow(IAppThemeModeService themeService)
		{
			var frame = new Frame { RequestedTheme = themeService.AppThemeMode };
			var window = new WindowEx(440, MinWindowHeight)
			{
				ExtendsContentIntoTitleBar = true,
				IsMaximizable = false,
				Content = frame,
				SystemBackdrop = new AppSystemBackdrop(true),
			};

			// Closing the window only hides this surface; the Status Center keeps tracking the operations.
			// If it was closed mid-operation, stay hidden until the next new operation arrives.
			window.Closed += (_, _) =>
			{
				_window = null;
				_userDismissed = Operations.Count > 0;
				CancelPendingShow();
			};

			var appWindow = window.AppWindow;
			appWindow.Title = "Files";
			appWindow.TitleBar.ExtendsContentIntoTitleBar = true;
			appWindow.SetIcon(AppLifecycleHelper.AppIconPath);
			frame.Navigate(typeof(FileOperationProgressPage), null, new SuppressNavigationTransitionInfo());

			Resize(appWindow);
			MoveNearMainWindow(appWindow);

			return window;
		}

		private static void UpdateWindowSize()
		{
			if (_window?.AppWindow is { } appWindow)
				Resize(appWindow);
		}

		private static void Resize(AppWindow appWindow)
		{
			var dpi = App.AppModel.AppWindowDPI;
			var height = Math.Clamp(
				TitleBarHeight + 40
					+ Operations.Count * ItemHeight
					+ Operations.Count(i => i.IsExpanded) * ExpandedItemHeight,
				MinWindowHeight, MaxWindowHeight);
			appWindow.Resize(new SizeInt32(
				Math.Max(1, Convert.ToInt32(WindowWidth * dpi)),
				Math.Max(1, Convert.ToInt32(height * dpi))));
		}

		private static void MoveNearMainWindow(AppWindow appWindow)
		{
			var main = MainWindow.Instance.AppWindow;
			var size = appWindow.Size;

			// Anchor to the lower-right corner of the main window, like the native dialog
			var x = main.Position.X + main.Size.Width - size.Width - 24;
			var y = main.Position.Y + main.Size.Height - size.Height - 24;

			var displayArea = DisplayArea.GetFromPoint(new PointInt32(x, y), DisplayAreaFallback.Nearest);
			x = Math.Clamp(x, displayArea.WorkArea.X, Math.Max(displayArea.WorkArea.X, displayArea.WorkArea.X + displayArea.WorkArea.Width - size.Width));
			y = Math.Clamp(y, displayArea.WorkArea.Y, Math.Max(displayArea.WorkArea.Y, displayArea.WorkArea.Y + displayArea.WorkArea.Height - size.Height));

			appWindow.Move(new PointInt32(x, y));
		}
	}
}
