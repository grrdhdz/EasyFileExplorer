// Copyright (c) Files Community
// Licensed under the MIT License.

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Files.App.Views
{
	/// <summary>
	/// Lists the file operations currently tracked by <see cref="FileOperationProgressWindow"/>.
	/// </summary>
	public sealed partial class FileOperationProgressPage : Page
	{
		public ObservableCollection<StatusCenterItem> Operations
			=> FileOperationProgressWindow.Operations;

		public FrameworkElement TitleBarElement => WindowTitleBar;

		public FileOperationProgressPage()
		{
			InitializeComponent();
		}
	}
}
