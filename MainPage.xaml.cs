using System;
using System.IO;
using System.Reflection.Metadata;
using System.Text;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Storage;

namespace MauiControlCenter;

public partial class MainPage : ContentPage
{
	string documentsPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
	string favourites = Environment.GetFolderPath(Environment.SpecialFolder.Favorites);
	string location = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);

	string[] files;
	string[] folders;

	private async Task OnOpenFileClicked(string filePath)
	{
		var path = filePath;

		if (!File.Exists(path))
		{
			await DisplayAlert("Error", "File not found.", "OK");
			// Still open file for now
			// return;
		}

		var ext = Path.GetExtension(path).ToLower();

		if (ext == ".png" || ext == ".jpg" || ext == ".jpeg")
		{
			PreviewImage.Source = ImageSource.FromFile(path);
			return;
		}
		else
		{
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
		}
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

		files = Directory.GetFiles(location);
		folders = Directory.GetDirectories(location);
	}
	public void UpdateFileFolders()
	{
		files = Directory.GetFiles(location);
		folders = Directory.GetDirectories(location);
	}

	//The default button, keep for now
	private void OnCounterClicked(object? sender, EventArgs? e)
	{
		CounterBtn.Text = location;
		UpdateFileFolders();

		MyStackLayout.Children.Clear();

		foreach (string item in folders)
		{
			// Get the file/folder name after the last '/'
			string name = Path.GetFileName(item);

			var button = new Button
			{
				Text = name,
				FontSize = 16,
				TextColor = Colors.White,
				BackgroundColor = Colors.DarkSlateGray,
				Padding = new Thickness(8, 4)
			};
			// Attach event handler (passing argument via lambda)
			button.Clicked += (s, e) => OnOpenFolderClicked(item);

			MyStackLayout.Children.Add(new Border
			{
				WidthRequest = 100,
                HeightRequest = 100,
                Margin = new Thickness(5),
                BackgroundColor = Colors.LightBlue,
				Content = button
			});
		}

		foreach (string filePath in files)
		{
			string name = Path.GetFileName(filePath);

			// Create button
			var button = new Button
			{
				Text = name,
				FontSize = 16,
				TextColor = Colors.White,
				BackgroundColor = Colors.DarkSlateGray,
				Padding = new Thickness(8, 4)
			};

			// Attach event handler (passing argument via lambda)
			button.Clicked += async (s, e) => OnOpenFileClicked(filePath);

			// Optional: wrap button in a border for styling
			MyStackLayout.Children.Add(new Border
			{
				Padding = new Thickness(10, 5),
				Margin = new Thickness(5),
				BackgroundColor = Colors.Black,
				Content = button
			});
		}

		SemanticScreenReader.Announce(CounterBtn.Text);
	}
}
