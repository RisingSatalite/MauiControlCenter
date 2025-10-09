using System;
using System.IO;
using System.Text;

namespace MauiControlCenter;

public partial class MainPage : ContentPage
{
	int count = 0;
	string document = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
	string favourites = Environment.GetFolderPath(Environment.SpecialFolder.Favorites);
	string location = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);

	public MainPage()
	{
		InitializeComponent();
	}

	private void OnCounterClicked(object? sender, EventArgs e)
	{
		CounterBtn.Text = location;

		SemanticScreenReader.Announce(CounterBtn.Text);
	}
}
