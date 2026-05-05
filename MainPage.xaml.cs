using System;
using System.IO;
using System.Reflection.Metadata;
using System.Text;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Storage;
using Microsoft.Maui.Controls;

#if WINDOWS
using Windows.Storage;
using System.Runtime.InteropServices.WindowsRuntime;
#endif

namespace MauiControlCenter;

public partial class MainPage : ContentPage
{
	//The inuse directory
	string location = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
	//Main folder paths
	string documentsPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
	string favourites = Environment.GetFolderPath(Environment.SpecialFolder.Favorites);
	string desktop = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
	string downloads = GetDownloadsPath();
	string pictures = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);
	string music = Environment.GetFolderPath(Environment.SpecialFolder.MyMusic);
	string videos = Environment.GetFolderPath(Environment.SpecialFolder.MyVideos);
	string startup = Environment.GetFolderPath(Environment.SpecialFolder.Startup);

	// Cancellation for incremental loading
	CancellationTokenSource? _loadCts;
	const int DefaultBatchSize = 64;
	string[] files = Array.Empty<string>();
	string[] folders = Array.Empty<string>();

	// Fields for splitter resizing
	double _initialLeftWidth;
	const double SplitterWidth = 8;
	const double MinPaneWidth = 120;

	// Collection backing the CollectionView
	private readonly ObservableCollection<FileItem> _items = new ObservableCollection<FileItem>();
	public ObservableCollection<FileItem> Items => _items;

	//Incase of edge cases
	private static string GetDownloadsPath()
	{
#if WINDOWS
		return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
#else
		return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Personal), "Downloads");
#endif
	}

	private async Task OnOpenFileClicked(string filePath)
	{
		var path = filePath;

		if (!File.Exists(path))
		{
			await DisplayAlert("Error", "File not found.", "OK");
			return;
		}

		var ext = Path.GetExtension(path).ToLower();

		if (ext == ".png" || ext == ".jpg" || ext == ".jpeg")
		{
			PreviewImage.Source = ImageSource.FromFile(path);
			// Still open file for now
			// return;
		}
		//else
		//{
			try
			{
				// Use MAUI Launcher to open the file with the default app on each platform
				var request = new OpenFileRequest
				{
					File = new ReadOnlyFile(path)
				};
				await Launcher.OpenAsync(request);
			}
			catch (Exception ex)
			{
				// Show an error if opening fails
				await DisplayAlert("Error", $"Unable to open file: {ex.Message}", "OK");
			}
		//}
	}

	private void OnOpenFolderClicked(string folderPath)
	{
		if (string.IsNullOrWhiteSpace(folderPath))
		{
			DisplayAlert("Error", "No folder path provided.", "OK");
			return;
		}

		if (!Directory.Exists(folderPath))
		{
			DisplayAlert("Error", "Folder not found.", "OK");
			return;
		}

		// ✅ Save the actual folder path, not the extension
		location = folderPath;
		OnCounterClicked(null, null);
	}

	// Used in the XAML
	private void OnOpenFolderClicked(object sender, EventArgs e)
	{
		if (sender is Button btn && btn.CommandParameter is string folderPath)
		{
			location = folderPath;
			OnCounterClicked(null, null);
			// DisplayAlert("Folder Path", folderPath, "OK");
			// open folder or do whatever with folderPath
		}
	}

	public MainPage()
	{
		InitializeComponent();

		Documents.CommandParameter = documentsPath;
		Favourites.CommandParameter = favourites;
		Desktop.CommandParameter = desktop;
		Downloads.CommandParameter = downloads;
		Music.CommandParameter = music;
		Video.CommandParameter = videos;
		Picture.CommandParameter = pictures;

		BindingContext = this;

		// Wire up CollectionView sizing so the grid span adapts to width
		var cv = this.FindByName<CollectionView>("FilesCollectionView");
		if (cv != null)
		{
			cv.SizeChanged += (s, e) =>
			{
				if (cv.Width <= 0) return;

				// Match these values to the DataTemplate
				const double itemContentWidth = 100; // DataTemplate VerticalStackLayout WidthRequest
				const double borderPaddingBothSides = 8; // Border Padding = 4 (left+right)
				const double borderMarginBothSides = 12; // Border Margin = 6 (left+right)
				const double horizontalSpacing = 12; // GridItemsLayout spacing

				double itemFullWidth = itemContentWidth + borderPaddingBothSides + borderMarginBothSides;

				int span = Math.Max(1, (int)Math.Floor((cv.Width + horizontalSpacing) / (itemFullWidth + horizontalSpacing)));

				// Update existing layout when possible to avoid churn
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

		// Do not enumerate folders synchronously on startup; load when requested.
	}

	// Called from XAML TapGestureRecognizer inside the CollectionView item template
	private async void OnItemTapped(object sender, EventArgs e)
	{
		var bo = sender as BindableObject;
		var item = bo?.BindingContext as FileItem;
		if (item == null) return;
		if (item.IsFolder)
		{
			OnOpenFolderClicked(item.Path);
		}
		else
		{
			await OnOpenFileClicked(item.Path);
		}
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
						// position overlay at current left width
						drag.TranslationX = _initialLeftWidth;
					});
				}
				break;

			case GestureStatus.Running:
				var newLeft = _initialLeftWidth + e.TotalX;
				var maxLeft = Math.Max(MinPaneWidth, MainGrid.Width - SplitterWidth - MinPaneWidth);
				if (newLeft < MinPaneWidth) newLeft = MinPaneWidth;
				if (newLeft > maxLeft) newLeft = maxLeft;

				// Move lightweight overlay for smooth feedback; commit actual layout on release
				if (drag != null)
				{
					MainThread.BeginInvokeOnMainThread(() => drag.TranslationX = newLeft);
				}
				break;

			case GestureStatus.Completed:
			case GestureStatus.Canceled:
				var available = Math.Max(1, MainGrid.Width - SplitterWidth);
				// compute final left pixels clamped
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

	public void UpdateFileFolders()
	{
		// keep legacy helper available
		files = Directory.GetFiles(location);
		folders = Directory.GetDirectories(location);
	}
	//The default button, keep for now
	private async void OnCounterClicked(object? sender, EventArgs? e)
	{
		CounterBtn.Text = location;
		// Cancel any previous load and start a new incremental load
		_loadCts?.Cancel();
		_loadCts = new CancellationTokenSource();
		try
		{
			await LoadFolderIncrementalAsync(location, _loadCts.Token).ConfigureAwait(false);
		}
		catch (OperationCanceledException)
		{
			// ignore
		}
		catch (Exception ex)
		{
			// show minimal error feedback on UI thread
			MainThread.BeginInvokeOnMainThread(async () => await DisplayAlert("Error", ex.Message, "OK"));
		}
	}

	// Incrementally enumerate and add folder/file UI in batches to keep UI responsive
	private async Task LoadFolderIncrementalAsync(string path, CancellationToken ct, int batchSize = DefaultBatchSize)
	{
		// Clear existing items quickly
		MainThread.BeginInvokeOnMainThread(() => _items.Clear());

		// Enumerate directories first, then files. Use Enumerate* to avoid materializing large arrays.
		var dirEnum = Directory.EnumerateDirectories(path).GetEnumerator();
		var fileEnum = Directory.EnumerateFiles(path).GetEnumerator();

		List<FileItem> batch = new List<FileItem>(batchSize);
		try
		{
			// Directories
			while (true)
			{
				ct.ThrowIfCancellationRequested();
				batch.Clear();
				for (int i = 0; i < batchSize && dirEnum.MoveNext(); i++)
				{
					batch.Add(new FileItem { Path = dirEnum.Current, Name = Path.GetFileName(dirEnum.Current), IsFolder = true });
				}

				if (batch.Count == 0) break;

				MainThread.BeginInvokeOnMainThread(() =>
				{
					foreach (var it in batch) _items.Add(it);
				});

				// yield to UI
				await Task.Yield();
			}

			// Files
			while (true)
			{
				ct.ThrowIfCancellationRequested();
				batch.Clear();
				for (int i = 0; i < batchSize && fileEnum.MoveNext(); i++)
				{
					var f = fileEnum.Current;
					var itm = new FileItem { Path = f, Name = Path.GetFileName(f), IsFolder = false };
					batch.Add(itm);
					_ = LoadAndApplyThumbnailAsync(itm);
				}

				if (batch.Count == 0) break;

				MainThread.BeginInvokeOnMainThread(() =>
				{
					foreach (var it in batch) _items.Add(it);
				});

				// yield to UI
				await Task.Yield();
			}
		}
		finally
		{
			dirEnum.Dispose();
			fileEnum.Dispose();
		}

		// Announce on UI thread when done
		MainThread.BeginInvokeOnMainThread(() => SemanticScreenReader.Announce(path));
	}

	private async Task<ImageSource?> GetThumbnailAsync(string path)
	{
#if WINDOWS
		try
		{
			var storageFile = await StorageFile.GetFileFromPathAsync(path);
			const uint requestedSize = 64;
			var thumb = await storageFile.GetThumbnailAsync(Windows.Storage.FileProperties.ThumbnailMode.SingleItem, requestedSize);
			if (thumb != null && thumb.Size > 0)
			{
				using (var rs = thumb.AsStreamForRead())
				using (var ms = new MemoryStream())
				{
					await rs.CopyToAsync(ms);
					var buffer = ms.ToArray();
					return ImageSource.FromStream(() => new MemoryStream(buffer));
				}
			}
		}
		catch
		{
			// ignore and fallback
		}
#endif
		return null;
	}
	private async Task LoadAndApplyThumbnailAsync(FileItem item)
	{
		try
		{
			var thumb = await GetThumbnailAsync(item.Path).ConfigureAwait(false);
			if (thumb != null)
			{
				MainThread.BeginInvokeOnMainThread(() => item.Thumbnail = thumb);
			}
		}
		catch
		{
			// ignore errors
		}
	}

}

// Simple data model for CollectionView items
public class FileItem : INotifyPropertyChanged
{
	public string Path { get; set; } = string.Empty;
	public string Name { get; set; } = string.Empty;
	public bool IsFolder { get; set; }

	ImageSource? _thumbnail;
	public ImageSource? Thumbnail
	{
		get => _thumbnail;
		set => SetProperty(ref _thumbnail, value);
	}

	public string Icon
	{
		get
		{
			if (IsFolder) return "📁";
			var ext = System.IO.Path.GetExtension(Path).ToLowerInvariant();
			if (ext == ".png" || ext == ".jpg" || ext == ".jpeg" || ext == ".bmp" || ext == ".gif") return "🖼️";
			return "📄";
		}
	}

	public event PropertyChangedEventHandler? PropertyChanged;

	protected bool SetProperty<T>(ref T backingStore, T value, [CallerMemberName] string? propertyName = null)
	{
		if (EqualityComparer<T>.Default.Equals(backingStore, value)) return false;
		backingStore = value;
		PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
		return true;
	}

	protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
	{
		PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
	}

}
