using System;
using System.IO;
using Microsoft.Maui.Controls;

namespace MauiControlCenter;

public partial class MainPage : ContentPage
{
	string documentsPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
	string favourites = Environment.GetFolderPath(Environment.SpecialFolder.Favorites);
	string desktop = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
	string downloads = GetDownloadsPath();
	string pictures = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);
	string music = Environment.GetFolderPath(Environment.SpecialFolder.MyMusic);
	string videos = Environment.GetFolderPath(Environment.SpecialFolder.MyVideos);

	double _initialLeftWidth;
	const double SplitterWidth = 8;
	const double MinPaneWidth = 120;

	public ExplorerViewModel Explorer { get; }

	private static string GetDownloadsPath()
	{
#if WINDOWS
		return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
#else
		return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Personal), "Downloads");
#endif
	}

	public MainPage()
	{
		InitializeComponent();
		Explorer = new ExplorerViewModel();

		Documents.CommandParameter = documentsPath;
		Favourites.CommandParameter = favourites;
		Desktop.CommandParameter = desktop;
		Downloads.CommandParameter = downloads;
		Music.CommandParameter = music;
		Video.CommandParameter = videos;
		Picture.CommandParameter = pictures;

		BindingContext = Explorer;
		_ = Explorer.NavigateToFolderAsync(documentsPath, addToHistory: false);

		var cv = this.FindByName<CollectionView>("FilesCollectionView");
		if (cv != null)
		{
			cv.SizeChanged += (s, e) =>
			{
				if (cv.Width <= 0) return;

				const double itemContentWidth = 100;
				const double borderPaddingBothSides = 8;
				const double borderMarginBothSides = 12;
				const double horizontalSpacing = 12;

				double itemFullWidth = itemContentWidth + borderPaddingBothSides + borderMarginBothSides;
				int span = Math.Max(1, (int)Math.Floor((cv.Width + horizontalSpacing) / (itemFullWidth + horizontalSpacing)));

				if (cv.ItemsLayout is GridItemsLayout grid)
				{
					if (grid.Span != span ||
						grid.HorizontalItemSpacing != horizontalSpacing ||
						grid.VerticalItemSpacing != 12)
					{
						grid.Span = span;
						grid.HorizontalItemSpacing = horizontalSpacing;
						grid.VerticalItemSpacing = 12;
					}
				}
				else
				{
					cv.ItemsLayout = new GridItemsLayout(span, ItemsLayoutOrientation.Vertical)
					{
						VerticalItemSpacing = 12,
						HorizontalItemSpacing = horizontalSpacing
					};
				}
			};
		}
	}

	private async void OnOpenFolderClicked(string folderPath)
	{
		await Explorer.NavigateToFolderAsync(folderPath);
	}

	private async void OnOpenFolderClicked(object sender, EventArgs e)
	{
		if (sender is Button btn && btn.CommandParameter is string folderPath)
		{
			await Explorer.NavigateToFolderAsync(folderPath);
		}
	}

	private async void OnBackClicked(object sender, EventArgs e)
	{
		await Explorer.GoBackAsync();
	}

	private async void OnUpClicked(object sender, EventArgs e)
	{
		await Explorer.GoUpAsync();
	}

	private async void OnRefreshClicked(object sender, EventArgs e)
	{
		await Explorer.RefreshAsync();
	}

	private void OnItemBindingContextChanged(object sender, EventArgs e)
	{
		if (sender is not Element element)
		{
			return;
		}

		var item = element.BindingContext as FileItem;
		if (item == null)
		{
			FlyoutBase.SetContextFlyout(element, null);
			return;
		}

		var flyout = new MenuFlyout();
		var openItem = new MenuFlyoutItem { Text = "Open" };
		openItem.Clicked += async (_, _) =>
		{
			Explorer.SelectItem(item);
			await Explorer.OpenItemAsync(item);
		};

		var renameItem = new MenuFlyoutItem { Text = "Rename" };
		renameItem.Clicked += async (_, _) =>
		{
			Explorer.SelectItem(item);
			await Explorer.RenameItemAsync(item);
		};

		var deleteItem = new MenuFlyoutItem { Text = "Delete" };
		deleteItem.Clicked += async (_, _) =>
		{
			Explorer.SelectItem(item);
			await Explorer.DeleteItemAsync(item);
		};

		flyout.Add(openItem);
		flyout.Add(renameItem);
		flyout.Add(deleteItem);
		FlyoutBase.SetContextFlyout(element, flyout);
	}

	private async void OnItemTapped(object sender, EventArgs e)
	{
		var bo = sender as BindableObject;
		var item = bo?.BindingContext as FileItem;
		if (item == null)
		{
			return;
		}

		Explorer.SelectItem(item);
		await Explorer.OpenItemAsync(item);
	}

	void OnSplitterPanUpdated(object sender, PanUpdatedEventArgs e)
	{
		var drag = this.FindByName<BoxView>("DragIndicator");
		switch (e.StatusType)
		{
			case GestureStatus.Started:
				_initialLeftWidth = LeftPane.Width;
				if (drag != null)
				{
					MainThread.BeginInvokeOnMainThread(() =>
					{
						drag.IsVisible = true;
						drag.TranslationX = _initialLeftWidth;
					});
				}
				break;

			case GestureStatus.Running:
				var newLeft = _initialLeftWidth + e.TotalX;
				var maxLeft = Math.Max(MinPaneWidth, MainGrid.Width - SplitterWidth - MinPaneWidth);
				if (newLeft < MinPaneWidth) newLeft = MinPaneWidth;
				if (newLeft > maxLeft) newLeft = maxLeft;

				if (drag != null)
				{
					MainThread.BeginInvokeOnMainThread(() => drag.TranslationX = newLeft);
				}
				break;

			case GestureStatus.Completed:
			case GestureStatus.Canceled:
				var available = Math.Max(1, MainGrid.Width - SplitterWidth);
				var finalLeft = Math.Max(MinPaneWidth, Math.Min(_initialLeftWidth + e.TotalX, MainGrid.Width - MinPaneWidth - SplitterWidth));
				var leftWeight = Math.Max(0.01, finalLeft / available);
				var rightWeight = Math.Max(0.01, (MainGrid.Width - finalLeft - SplitterWidth) / available);
				MainGrid.ColumnDefinitions[0].Width = new GridLength(leftWeight, GridUnitType.Star);
				MainGrid.ColumnDefinitions[2].Width = new GridLength(rightWeight, GridUnitType.Star);

				if (drag != null)
				{
					MainThread.BeginInvokeOnMainThread(() => drag.IsVisible = false);
				}
				break;
		}
	}
}
