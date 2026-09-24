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
		private const int ItemHeight = 190;
		private const int MinWindowHeight = 220;
		private const int MaxWindowHeight = 700;

		private static readonly StatusCenterViewModel _viewModel = Ioc.Default.GetRequiredService<StatusCenterViewModel>();
		private static WindowEx? _window;
		private static bool _initialized;

		internal static ObservableCollection<StatusCenterItem> Operations { get; } = [];

		public static void Initialize()
		{
			if (_initialized)
				return;
			_initialized = true;

			_viewModel.StatusCenterItems.CollectionChanged += OnStatusCenterItemsChanged;
			foreach (var item in _viewModel.StatusCenterItems)
				TrackIfActive(item);
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

		private static void TrackIfActive(StatusCenterItem item)
		{
			if (!TracksOperation(item) || Operations.Contains(item))
				return;

			Operations.Add(item);
			item.PropertyChanged += OnItemPropertyChanged;

			UpdateWindowSize();
			Show();
		}

		private static void OnItemPropertyChanged(object? sender, PropertyChangedEventArgs e)
		{
			if (e.PropertyName is nameof(StatusCenterItem.FileSystemOperationReturnResult)
				&& sender is StatusCenterItem item
				&& item.FileSystemOperationReturnResult is not ReturnResult.InProgress)
			{
				Untrack(item);
			}
		}

		private static void Untrack(StatusCenterItem item)
		{
			if (!Operations.Remove(item))
				return;

			item.PropertyChanged -= OnItemPropertyChanged;

			// Auto-close once nothing is in flight; closing the window never cancels operations
			if (Operations.Count == 0)
				_window?.Close();
			else
				UpdateWindowSize();
		}

		private static void Show()
		{
			var themeService = Ioc.Default.GetRequiredService<IAppThemeModeService>();
			var window = _window ??= CreateWindow(themeService);
			if (window.Content is Frame frame)
				frame.RequestedTheme = themeService.AppThemeMode;
			themeService.SetAppThemeMode(window, window.AppWindow.TitleBar, themeService.AppThemeMode, callThemeModeChangedEvent: false);

			window.AppWindow.Show();
			window.Activate();
		}

		private static WindowEx CreateWindow(IAppThemeModeService themeService)
		{
			var frame = new Frame { RequestedTheme = themeService.AppThemeMode };
			var window = new WindowEx(440, MinWindowHeight)
			{
				IsMaximizable = false,
				Content = frame,
				SystemBackdrop = new AppSystemBackdrop(true),
			};

			// Closing the window only hides this surface; the Status Center keeps tracking the operations
			window.Closed += (_, _) => _window = null;

			var appWindow = window.AppWindow;
			appWindow.Title = "Files";
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
			var height = Math.Clamp(56 + Operations.Count * ItemHeight, MinWindowHeight, MaxWindowHeight);
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
