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

		SemanticScreenReader.Announce(CounterBtn.Text);
	}
}
