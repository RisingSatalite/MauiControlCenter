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
	string pictures = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);
	string music = Environment.GetFolderPath(Environment.SpecialFolder.MyMusic);
	string videos = Environment.GetFolderPath(Environment.SpecialFolder.MyVideos);
	string startup = Environment.GetFolderPath(Environment.SpecialFolder.Startup);

	string[] files;
	string[] folders;

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
		Music.CommandParameter = music;
		Video.CommandParameter = videos;
		Picture.CommandParameter = pictures;

		files = Directory.GetFiles(location);
		folders = Directory.GetDirectories(location);
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
		UpdateFileFolders();

		MyStackLayout.Children.Clear();

		// Folders: show folder icon above name
		foreach (string item in folders)
		{
			string name = Path.GetFileName(item);

			var iconLabel = new Label
			{
				Text = "📁",
				FontSize = 48,
				HorizontalOptions = LayoutOptions.Center,
				VerticalOptions = LayoutOptions.Center
			};

			var nameLabel = new Label
			{
				Text = name,
				FontSize = 12,
				HorizontalTextAlignment = TextAlignment.Center,
				LineBreakMode = LineBreakMode.TailTruncation
			};

			var stack = new VerticalStackLayout
			{
				WidthRequest = 100,
				Padding = new Thickness(6),
				Children = { iconLabel, nameLabel }
			};

			var border = new Border
			{
				Padding = new Thickness(4),
				Margin = new Thickness(6),
				BackgroundColor = Colors.Transparent,
				Content = stack
			};

			var tap = new TapGestureRecognizer();
			// capture item in lambda
			tap.Tapped += (s, e) => OnOpenFolderClicked(item);
			border.GestureRecognizers.Add(tap);

			MyStackLayout.Children.Add(border);
		}

		// Files: show thumbnail for images, Windows native thumbnails when available, generic icon otherwise
		foreach (string filePath in files)
		{
			string name = Path.GetFileName(filePath);
			string ext = Path.GetExtension(filePath).ToLowerInvariant();

			// Create a lightweight placeholder immediately (keeps UI fast)
			View placeholder = new Label
			{
				Text = ext == ".png" || ext == ".jpg" || ext == ".jpeg" || ext == ".bmp" || ext == ".gif" ? "🖼️" : "📄",
				FontSize = 36,
				HorizontalOptions = LayoutOptions.Center,
				VerticalOptions = LayoutOptions.Center
			};

			var nameLabel = new Label
			{
				Text = name,
				FontSize = 12,
				HorizontalTextAlignment = TextAlignment.Center,
				LineBreakMode = LineBreakMode.TailTruncation
			};

			var stack = new VerticalStackLayout
			{
				WidthRequest = 100,
				Padding = new Thickness(6),
				Children = { placeholder, nameLabel }
			};

			var border = new Border
			{
				Padding = new Thickness(4),
				Margin = new Thickness(6),
				BackgroundColor = Colors.Transparent,
				Content = stack
			};

			var tap = new TapGestureRecognizer();
			// open file on tap
			tap.Tapped += async (s, e) => await OnOpenFileClicked(filePath);
			border.GestureRecognizers.Add(tap);

			MyStackLayout.Children.Add(border);

			// Start loading thumbnail in background and replace placeholder when ready
			_ = LoadAndApplyThumbnailAsync(filePath, stack);
		}

		SemanticScreenReader.Announce(CounterBtn.Text);
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
