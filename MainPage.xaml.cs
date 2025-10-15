using System;
using System.IO;
using System.Reflection.Metadata;
using System.Text;

namespace MauiControlCenter;

public partial class MainPage : ContentPage
{
	string document = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
	string favourites = Environment.GetFolderPath(Environment.SpecialFolder.Favorites);
	string location = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);

	string[] files;
	string[] folders;
	private void OnOpenFileClicked(string filePath)
	{
		var path = filePath;

		if (!File.Exists(path))
		{
			DisplayAlert("Error", "File not found.", "OK");
			return;
		}

		var ext = Path.GetExtension(path).ToLower();

		if (ext == ".png" || ext == ".jpg" || ext == ".jpeg")
		{
			PreviewImage.Source = ImageSource.FromFile(path);
		}
		else
		{
			// Open in default app
			var psi = new System.Diagnostics.ProcessStartInfo(path)
			{
				UseShellExecute = true
			};
			System.Diagnostics.Process.Start(psi);
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

	public MainPage()
	{
		InitializeComponent();
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
				Padding = new Thickness(10, 5),
				Margin = new Thickness(5),
				BackgroundColor = Colors.Black,
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
			button.Clicked += (s, e) => OnOpenFileClicked(filePath);

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
