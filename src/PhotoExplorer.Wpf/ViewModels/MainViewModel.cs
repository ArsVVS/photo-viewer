using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PhotoExplorer.Core;
using PhotoExplorer.Core.Models;

namespace PhotoExplorer.Wpf.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly FolderBrowser _browser;
    private readonly ThumbnailLoader _loader;

    // История переходов для кнопок «Назад» и «Вперёд»
    private readonly Stack<string> _backHistory = new();
    private readonly Stack<string> _forwardHistory = new();

    // Все файлы текущей папки
    private List<ImageFileInfo> _files = [];

    // Быстрый поиск плитки по пути файла (для загрузчика миниатюр)
    private Dictionary<string, ThumbnailItemViewModel> _itemsByPath = [];

    // Корни дерева папок
    public ObservableCollection<FolderItemViewModel> RootItems { get; } = [];

    // Плитки в сетке миниатюр
    [ObservableProperty]
    public partial ObservableCollection<ThumbnailItemViewModel> Items { get; set; } = [];

    [ObservableProperty]
    public partial string CurrentFolder { get; set; } = "";

    // Текст в адресной строке (можно править вручную)
    [ObservableProperty]
    public partial string AddressText { get; set; } = "";

    // Размер миниатюр по длинной стороне
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TileWidth))]
    public partial int ThumbnailSize { get; set; }

    // Ширина плитки чуть больше миниатюры, чтобы влезла подпись
    public int TileWidth => ThumbnailSize + 16;

    // Сколько миниатюр загружено
    [ObservableProperty]
    public partial int LoadedCount { get; set; }

    public MainViewModel()
    {
        _browser = new FolderBrowser(App.Settings);

        _loader = new ThumbnailLoader(App.Cache);
        _loader.ThumbnailReady += Loader_ThumbnailReady;
        _loader.ProgressChanged += (done, _) => Application.Current.Dispatcher.BeginInvoke(() => LoadedCount = done);

        ThumbnailSize = App.Settings.ThumbnailSize;

        LoadTree();
    }

    // Открывает стартовую папку из настроек
    public void OpenStartFolder()
    {
        var folder = App.Settings.StartFolderMode == StartFolderMode.Selected
            ? App.Settings.StartFolder
            : App.Settings.LastFolder;
        if (string.IsNullOrEmpty(folder) || !Directory.Exists(folder))
            folder = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);
        NavigateTo(folder);
    }

    // Строит дерево: «Этот компьютер» со списком дисков
    private void LoadTree()
    {
        RootItems.Clear();
        var computer = new FolderItemViewModel("Этот компьютер", "", Icons.Computer, null) { IsExpanded = true };
        foreach (var drive in _browser.GetDrives())
            computer.Children.Add(new FolderItemViewModel(drive.Name, drive.Path, Icons.Drive, _browser, drive.HasSubfolders));
        RootItems.Add(computer);
    }

    // Открывает папку. addToHistory = false – для переходов «Назад»/«Вперёд»
    public void NavigateTo(string path, bool addToHistory = true)
    {
        if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path))
            return;

        path = Path.GetFullPath(path);
        if (addToHistory && CurrentFolder != "" && !string.Equals(CurrentFolder, path, StringComparison.OrdinalIgnoreCase))
        {
            _backHistory.Push(CurrentFolder);
            _forwardHistory.Clear();
        }

        CurrentFolder = path;
        AddressText = path;
        App.Settings.LastFolder = path;

        BackCommand.NotifyCanExecuteChanged();
        ForwardCommand.NotifyCanExecuteChanged();
        UpCommand.NotifyCanExecuteChanged();

        LoadFolder();
    }

    // Перечитывает файлы текущей папки
    private void LoadFolder()
    {
        _files = _browser.GetImages(CurrentFolder);
        ShowFiles();
    }

    // Показывает файлы в сетке и запускает загрузку миниатюр
    private void ShowFiles()
    {
        var sorted = ImageSorter.Sort(_files, SortField.Name);
        var items = sorted.Select(f => new ThumbnailItemViewModel(f)).ToList();

        _itemsByPath = items.ToDictionary(i => i.File.FullPath, StringComparer.OrdinalIgnoreCase);
        // Новую коллекцию целиком – так быстрее, чем добавлять по одной
        Items = new ObservableCollection<ThumbnailItemViewModel>(items);
        LoadThumbnails();
    }

    private void LoadThumbnails()
    {
        foreach (var item in Items)
            item.IsLoading = true;
        LoadedCount = 0;
        _ = _loader.LoadAsync(Items.Select(i => i.File).ToList(), ThumbnailSize);
    }

    // Вызывается из фонового потока, когда готова очередная миниатюра
    private void Loader_ThumbnailReady(string path, byte[]? data)
    {
        var image = data != null ? ImageHelper.FromBytes(data) : null;
        Application.Current.Dispatcher.BeginInvoke(() =>
        {
            if (_itemsByPath.TryGetValue(path, out var item))
                item.SetThumbnail(image);
        });
    }

    // Сменили размер миниатюр – загружаем их заново
    partial void OnThumbnailSizeChanged(int value)
    {
        if (Items.Count > 0)
            LoadThumbnails();
    }

    private bool CanGoBack() => _backHistory.Count > 0;

    [RelayCommand(CanExecute = nameof(CanGoBack))]
    private void Back()
    {
        _forwardHistory.Push(CurrentFolder);
        NavigateTo(_backHistory.Pop(), addToHistory: false);
    }

    private bool CanGoForward() => _forwardHistory.Count > 0;

    [RelayCommand(CanExecute = nameof(CanGoForward))]
    private void Forward()
    {
        _backHistory.Push(CurrentFolder);
        NavigateTo(_forwardHistory.Pop(), addToHistory: false);
    }

    private bool CanGoUp() => CurrentFolder != "" && Directory.GetParent(CurrentFolder) != null;

    [RelayCommand(CanExecute = nameof(CanGoUp))]
    private void Up()
    {
        var parent = Directory.GetParent(CurrentFolder);
        if (parent != null)
            NavigateTo(parent.FullName);
    }

    // Переход по адресу, введённому вручную
    [RelayCommand]
    private void GoToAddress()
    {
        var path = AddressText.Trim().Trim('"');
        if (Directory.Exists(path))
            NavigateTo(path);
        else
            MessageBox.Show($"Папка не найдена:\n{path}", "Фото проводник", MessageBoxButton.OK, MessageBoxImage.Warning);
    }
}
