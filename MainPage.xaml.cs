using System;
using System.IO;
using System.Reflection.Metadata;
using System.Text;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Storage;

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
	string[] files;
	string[] folders;

	// Fields for splitter resizing
	double _initialLeftWidth;
	const double SplitterWidth = 8;
	const double MinPaneWidth = 120;

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

		// Do not enumerate folders synchronously on startup; load when requested.
	}

	void OnSplitterPanUpdated(object sender, PanUpdatedEventArgs e)
	{
		switch (e.StatusType)
		{
			case GestureStatus.Started:
				_initialLeftWidth = LeftPane.Width;
				break;

			case GestureStatus.Running:
				var newLeft = _initialLeftWidth + e.TotalX;
				var maxLeft = Math.Max(MinPaneWidth, MainGrid.Width - SplitterWidth - MinPaneWidth);
				if (newLeft < MinPaneWidth) newLeft = MinPaneWidth;
				if (newLeft > maxLeft) newLeft = maxLeft;

				MainGrid.ColumnDefinitions[0].Width = new GridLength(newLeft, GridUnitType.Absolute);
				MainGrid.ColumnDefinitions[2].Width = new GridLength(Math.Max(0, MainGrid.Width - newLeft - SplitterWidth), GridUnitType.Absolute);
				break;

			case GestureStatus.Completed:
			case GestureStatus.Canceled:
				var available = Math.Max(1, MainGrid.Width - SplitterWidth);
				var leftPixels = MainGrid.ColumnDefinitions[0].Width.IsAbsolute ? MainGrid.ColumnDefinitions[0].Width.Value : LeftPane.Width;
				var rightPixels = MainGrid.ColumnDefinitions[2].Width.IsAbsolute ? MainGrid.ColumnDefinitions[2].Width.Value : (MainGrid.Width - leftPixels - SplitterWidth);
				var leftWeight = Math.Max(0.01, leftPixels / available);
				var rightWeight = Math.Max(0.01, rightPixels / available);
				MainGrid.ColumnDefinitions[0].Width = new GridLength(leftWeight, GridUnitType.Star);
				MainGrid.ColumnDefinitions[2].Width = new GridLength(rightWeight, GridUnitType.Star);
				break;
		}
	}
	public void UpdateFileFolders()
	{
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
		// Clear existing items on UI thread quickly
		MainThread.BeginInvokeOnMainThread(() => MyStackLayout.Children.Clear());

		// Helper to create folder UI
		UIElement CreateFolderElement(string folderPath)
		{
			string name = Path.GetFileName(folderPath);
			var iconLabel = new Label { Text = "📁", FontSize = 48, HorizontalOptions = LayoutOptions.Center, VerticalOptions = LayoutOptions.Center };
			var nameLabel = new Label { Text = name, FontSize = 12, HorizontalTextAlignment = TextAlignment.Center, LineBreakMode = LineBreakMode.TailTruncation };
			var stack = new VerticalStackLayout { WidthRequest = 100, Padding = new Thickness(6), Children = { iconLabel, nameLabel } };
			var border = new Border { Padding = new Thickness(4), Margin = new Thickness(6), BackgroundColor = Colors.Transparent, Content = stack };
			var tap = new TapGestureRecognizer();
			tap.Tapped += (s, e) => OnOpenFolderClicked(folderPath);
			border.GestureRecognizers.Add(tap);
			return border;
		}

		// Helper to create file UI and kick off thumbnail loading
		UIElement CreateFileElement(string filePath)
		{
			string name = Path.GetFileName(filePath);
			string ext = Path.GetExtension(filePath).ToLowerInvariant();
			View placeholder = new Label { Text = ext == ".png" || ext == ".jpg" || ext == ".jpeg" || ext == ".bmp" || ext == ".gif" ? "🖼️" : "📄", FontSize = 36, HorizontalOptions = LayoutOptions.Center, VerticalOptions = LayoutOptions.Center };
			var nameLabel = new Label { Text = name, FontSize = 12, HorizontalTextAlignment = TextAlignment.Center, LineBreakMode = LineBreakMode.TailTruncation };
			var stack = new VerticalStackLayout { WidthRequest = 100, Padding = new Thickness(6), Children = { placeholder, nameLabel } };
			var border = new Border { Padding = new Thickness(4), Margin = new Thickness(6), BackgroundColor = Colors.Transparent, Content = stack };
			var tap = new TapGestureRecognizer();
			tap.Tapped += async (s, e) => await OnOpenFileClicked(filePath);
			border.GestureRecognizers.Add(tap);
			_ = LoadAndApplyThumbnailAsync(filePath, stack);
			return border;
		}

		// Enumerate directories first, then files. Use Enumerate* to avoid materializing large arrays.
		var dirEnum = Directory.EnumerateDirectories(path).GetEnumerator();
		var fileEnum = Directory.EnumerateFiles(path).GetEnumerator();

		List<UIElement> batch = new List<UIElement>(batchSize);
		try
		{
			// Directories
			while (true)
			{
				ct.ThrowIfCancellationRequested();
				batch.Clear();
				for (int i = 0; i < batchSize && dirEnum.MoveNext(); i++)
				{
					batch.Add(CreateFolderElement(dirEnum.Current));
				}

				if (batch.Count == 0) break;

				MainThread.BeginInvokeOnMainThread(() =>
				{
					foreach (var v in batch) MyStackLayout.Children.Add(v);
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
					batch.Add(CreateFileElement(fileEnum.Current));
				}

				if (batch.Count == 0) break;

				MainThread.BeginInvokeOnMainThread(() =>
				{
					foreach (var v in batch) MyStackLayout.Children.Add(v);
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

	private async Task LoadAndApplyThumbnailAsync(string filePath, VerticalStackLayout stack)
	{
		try
		{
			var thumb = await GetThumbnailAsync(filePath).ConfigureAwait(false);
			if (thumb != null)
			{
				Microsoft.Maui.ApplicationModel.MainThread.BeginInvokeOnMainThread(() =>
				{
					var img = new Image
					{
						Source = thumb,
						WidthRequest = 64,
						HeightRequest = 64,
						Aspect = Aspect.AspectFill,
						HorizontalOptions = LayoutOptions.Center
					};
					// Replace first child (placeholder) with the loaded image
					if (stack.Children.Count > 0)
					{
						stack.Children[0] = img;
					}
				});
			}
			else
			{
				// no thumbnail: do nothing, placeholder remains
			}
		}
		catch
		{
			// ignore errors and keep placeholder
		}
	}
}
