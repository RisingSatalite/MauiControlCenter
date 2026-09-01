using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Storage;

namespace MauiControlCenter;

public class ExplorerViewModel : INotifyPropertyChanged
{
    private readonly Stack<string> _backStack = new();
    private CancellationTokenSource? _loadCts;

    private readonly ObservableCollection<FileItem> _items = new();
    public ObservableCollection<FileItem> Items => _items;

    public string CurrentPath { get; private set; } = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);

    public bool CanGoBack => _backStack.Count > 0;

    public bool CanGoUp => Directory.Exists(Path.GetDirectoryName(CurrentPath));

    public event PropertyChangedEventHandler? PropertyChanged;

    public async Task NavigateToFolderAsync(string folderPath, bool addToHistory = true)
    {
        if (string.IsNullOrWhiteSpace(folderPath))
        {
            await ShowAlert("Error", "No folder path provided.", "OK");
            return;
        }

        var normalizedPath = Path.GetFullPath(folderPath);
        if (!Directory.Exists(normalizedPath))
        {
            await ShowAlert("Error", "Folder not found.", "OK");
            return;
        }

        if (addToHistory && !string.Equals(CurrentPath, normalizedPath, StringComparison.OrdinalIgnoreCase))
        {
            _backStack.Push(CurrentPath);
        }

        CurrentPath = normalizedPath;
        OnPropertyChanged(nameof(CurrentPath));
        OnPropertyChanged(nameof(CanGoBack));
        OnPropertyChanged(nameof(CanGoUp));

        await LoadFolderAsync(CurrentPath);
    }

    public async Task GoBackAsync()
    {
        if (_backStack.Count == 0)
        {
            await ShowAlert("Navigation", "No previous folder.", "OK");
            return;
        }

        var previousFolder = _backStack.Pop();
        CurrentPath = previousFolder;
        OnPropertyChanged(nameof(CurrentPath));
        OnPropertyChanged(nameof(CanGoBack));
        OnPropertyChanged(nameof(CanGoUp));

        await LoadFolderAsync(CurrentPath);
    }

    public async Task GoUpAsync()
    {
        var parent = Directory.GetParent(CurrentPath);
        if (parent == null)
        {
            await ShowAlert("Navigation", "You are already at the root of this folder.", "OK");
            return;
        }

        await NavigateToFolderAsync(parent.FullName);
    }

    public async Task RefreshAsync()
    {
        if (string.IsNullOrWhiteSpace(CurrentPath))
        {
            return;
        }

        await LoadFolderAsync(CurrentPath);
    }

    public async Task OpenItemAsync(FileItem? item)
    {
        if (item == null)
        {
            return;
        }

        if (item.IsFolder)
        {
            await NavigateToFolderAsync(item.Path);
            return;
        }

        if (!File.Exists(item.Path))
        {
            await ShowAlert("Error", "File not found.", "OK");
            return;
        }

        try
        {
            var request = new OpenFileRequest
            {
                File = new ReadOnlyFile(item.Path)
            };

            await Launcher.OpenAsync(request);
        }
        catch (Exception ex)
        {
            await ShowAlert("Error", $"Unable to open file: {ex.Message}", "OK");
        }
    }

    private async Task LoadFolderAsync(string path)
    {
        _loadCts?.Cancel();
        _loadCts = new CancellationTokenSource();
        var token = _loadCts.Token;

        try
        {
            var items = await Task.Run(() =>
            {
                var directories = Directory.EnumerateDirectories(path)
                    .OrderBy(d => Path.GetFileName(d), StringComparer.OrdinalIgnoreCase)
                    .Select(d => new FileItem
                    {
                        Path = d,
                        Name = Path.GetFileName(d) ?? d,
                        IsFolder = true
                    })
                    .ToList();

                var files = Directory.EnumerateFiles(path)
                    .OrderBy(f => Path.GetFileName(f), StringComparer.OrdinalIgnoreCase)
                    .Select(f => new FileItem
                    {
                        Path = f,
                        Name = Path.GetFileName(f) ?? f,
                        IsFolder = false
                    })
                    .ToList();

                return directories.Concat(files).ToList();
            }, token);

            if (token.IsCancellationRequested)
            {
                return;
            }

            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                _items.Clear();
                foreach (var item in items)
                {
                    _items.Add(item);
                }
            });

            foreach (var item in items.Where(i => !i.IsFolder))
            {
                _ = LoadThumbnailAsync(item);
            }
        }
        catch (OperationCanceledException)
        {
            // Ignore canceled navigation requests.
        }
        catch (Exception ex)
        {
            await ShowAlert("Error", ex.Message, "OK");
        }
    }

    private async Task LoadThumbnailAsync(FileItem item)
    {
        try
        {
            var thumb = await GetThumbnailAsync(item.Path).ConfigureAwait(false);
            if (thumb != null)
            {
                await MainThread.InvokeOnMainThreadAsync(() => item.Thumbnail = thumb);
            }
        }
        catch
        {
            // Ignore preview failures and keep the fallback icon.
        }
    }

    private async Task<ImageSource?> GetThumbnailAsync(string path)
    {
#if WINDOWS
        try
        {
            var storageFile = await Windows.Storage.StorageFile.GetFileFromPathAsync(path);
            const uint requestedSize = 64;
            var thumb = await storageFile.GetThumbnailAsync(Windows.Storage.FileProperties.ThumbnailMode.SingleItem, requestedSize);
            if (thumb != null && thumb.Size > 0)
            {
                using var stream = thumb.AsStreamForRead();
                using var memory = new MemoryStream();
                await stream.CopyToAsync(memory);
                var buffer = memory.ToArray();
                return ImageSource.FromStream(() => new MemoryStream(buffer));
            }
        }
        catch
        {
            // Ignore unsupported items.
        }
#endif
        return null;
    }

    private static async Task ShowAlert(string title, string message, string accept)
    {
        var page = Application.Current?.MainPage;
        if (page != null)
        {
            await page.DisplayAlert(title, message, accept);
        }
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

public class FileItem : INotifyPropertyChanged
{
    public string Path { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public bool IsFolder { get; set; }

    private ImageSource? _thumbnail;
    public ImageSource? Thumbnail
    {
        get => _thumbnail;
        set => SetProperty(ref _thumbnail, value);
    }

    //Old
    public string Icon
    {
        get
        {
            if (IsFolder)
            {
                return "📁";
            }

            //var ext = Path.GetExtension(Path).ToLowerInvariant();
            //if (ext == ".png" || ext == ".jpg" || ext == ".jpeg" || ext == ".bmp" || ext == ".gif")
            //{
                //return "🖼️";
            //}

            return "📄";
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected bool SetProperty<T>(ref T backingStore, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(backingStore, value))
        {
            return false;
        }

        backingStore = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        return true;
    }
}
