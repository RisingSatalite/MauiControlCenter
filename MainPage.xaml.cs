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

	private void OnCounterClicked(object? sender, EventArgs e)
	{
		CounterBtn.Text = location;
		UpdateFileFolders();

		MyStackLayout.Children.Clear();

        // Add a label for each string
        foreach (string item in files)
        {
			// Get the file/folder name after the last '/'
			string name = Path.GetFileName(item);
			MyStackLayout.Children.Add(new Label
			{
				Text = name,
				FontSize = 20,
				TextColor = Colors.White
			});
        }

		SemanticScreenReader.Announce(CounterBtn.Text);
	}
}
