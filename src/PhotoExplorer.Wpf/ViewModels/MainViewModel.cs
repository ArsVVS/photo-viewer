using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PhotoExplorer.Core;
using PhotoExplorer.Core.Models;

namespace PhotoExplorer.Wpf.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly FolderBrowser _browser;
    private readonly ThumbnailLoader _loader;

    // Размер картинки для панели предпросмотра
    private const int PreviewSize = 600;

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
    [NotifyPropertyChangedFor(nameof(ProgressText))]
    public partial int LoadedCount { get; set; }

    // Выделенные плитки
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SelectedText))]
    public partial List<ThumbnailItemViewModel> SelectedItems { get; set; } = [];

    public ThumbnailItemViewModel? SelectedItem => SelectedItems.FirstOrDefault();

    // Предпросмотр выбранного изображения
    [ObservableProperty]
    public partial ImageSource? PreviewImage { get; set; }

    [ObservableProperty]
    public partial string PreviewInfo { get; set; } = "";

    // Тексты для строки статуса
    public string CountText => $"Изображений: {Items.Count}";
    public string TotalSizeText => $"Общий размер: {Formatting.FormatSize(Items.Sum(i => i.File.Size))}";
    public string SelectedText => $"Выделено: {SelectedItems.Count}";
    public string ProgressText => IsSearching ? "Идёт поиск..."
        : LoadedCount < _loadTotal ? $"Миниатюры: {LoadedCount} из {_loadTotal}" : "Миниатюры загружены";

    // Сколько миниатюр в текущей загрузке
    private int _loadTotal;

    // Сортировка и фильтр по типу
    [ObservableProperty]
    public partial SortField SortField { get; set; } = SortField.Name;

    [ObservableProperty]
    public partial bool SortDescending { get; set; }

    [ObservableProperty]
    public partial List<string> TypeFilters { get; set; }

    [ObservableProperty]
    public partial string TypeFilter { get; set; }

    // Поиск по имени
    [ObservableProperty]
    public partial string SearchText { get; set; } = "";

    [ObservableProperty]
    public partial bool SearchRecursive { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ProgressText))]
    public partial bool IsSearching { get; set; }

    private readonly DispatcherTimer _searchTimer;
    private CancellationTokenSource? _searchCts;

    // Когда поле поиска очищается при переходе в другую папку, искать не нужно
    private bool _suppressSearch;

    // Номер последнего запроса предпросмотра – чтобы старая картинка не перезаписала новую
    private int _previewVersion;

    public MainViewModel()
    {
        _browser = new FolderBrowser(App.Settings);

        _loader = new ThumbnailLoader(App.Cache);
        _loader.ThumbnailReady += Loader_ThumbnailReady;
        _loader.ProgressChanged += (done, total) => Application.Current.Dispatcher.BeginInvoke(() =>
        {
            _loadTotal = total;
            LoadedCount = done;
        });

        TypeFilters = ["Все типы", .. App.Settings.Extensions.Select(e => e.TrimStart('.').ToUpperInvariant())];
        TypeFilter = TypeFilters[0];

        // Поиск запускается, когда пользователь перестал печатать
        _searchTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(400) };
        _searchTimer.Tick += (_, _) =>
        {
            _searchTimer.Stop();
            _ = RunSearchAsync();
        };

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

        // В новой папке начинаем без поиска
        CancelSearch();
        _suppressSearch = true;
        SearchText = "";
        SearchRecursive = false;
        _suppressSearch = false;

        LoadFolder();
    }

    // Перечитывает файлы текущей папки
    private void LoadFolder()
    {
        _files = _browser.GetImages(CurrentFolder);
        // Папку читаем заново – старые плитки не используем
        _itemsByPath.Clear();
        ShowFiles();
    }

    // Показывает файлы в сетке (с фильтром и сортировкой) и загружает недостающие миниатюры
    private void ShowFiles()
    {
        var filtered = _files.Where(PassesTypeFilter);
        var sorted = ImageSorter.Sort(filtered, SortField, SortDescending);

        // Уже загруженные плитки используем повторно, чтобы не грузить миниатюры заново
        var items = sorted.Select(f => _itemsByPath.GetValueOrDefault(f.FullPath) ?? new ThumbnailItemViewModel(f)).ToList();

        _itemsByPath = items.ToDictionary(i => i.File.FullPath, StringComparer.OrdinalIgnoreCase);
        // Новую коллекцию целиком – так быстрее, чем добавлять по одной
        Items = new ObservableCollection<ThumbnailItemViewModel>(items);
        SetSelection([]);
        UpdateCounters();
        LoadThumbnails(onlyMissing: true);
    }

    private void UpdateCounters()
    {
        OnPropertyChanged(nameof(CountText));
        OnPropertyChanged(nameof(TotalSizeText));
    }

    private bool PassesTypeFilter(ImageFileInfo file)
    {
        if (TypeFilter == TypeFilters[0])
            return true;
        return string.Equals(file.Extension.TrimStart('.'), TypeFilter, StringComparison.OrdinalIgnoreCase);
    }

    partial void OnSortFieldChanged(SortField value) => ShowFiles();

    partial void OnSortDescendingChanged(bool value) => ShowFiles();

    partial void OnTypeFilterChanged(string value)
    {
        // Первый раз вызывается из конструктора, когда папка ещё не открыта
        if (CurrentFolder != "")
            ShowFiles();
    }

    partial void OnSearchTextChanged(string value) => RestartSearchTimer();

    partial void OnSearchRecursiveChanged(bool value) => RestartSearchTimer();

    private void RestartSearchTimer()
    {
        if (_suppressSearch)
            return;
        _searchTimer.Stop();
        _searchTimer.Start();
    }

    private void CancelSearch()
    {
        _searchTimer.Stop();
        _searchCts?.Cancel();
        _searchCts = null;
        IsSearching = false;
    }

    // Поиск в текущей папке (и во вложенных, если стоит флажок).
    // Найденные файлы появляются в сетке сразу, по мере нахождения.
    private async Task RunSearchAsync()
    {
        CancelSearch();
        if (string.IsNullOrWhiteSpace(SearchText) && !SearchRecursive)
        {
            LoadFolder();
            return;
        }

        var cts = new CancellationTokenSource();
        _searchCts = cts;
        IsSearching = true;

        _loader.Cancel();
        _files = [];
        _itemsByPath = new Dictionary<string, ThumbnailItemViewModel>(StringComparer.OrdinalIgnoreCase);
        Items = [];
        SetSelection([]);

        var options = new SearchOptions { Name = SearchText, Recursive = SearchRecursive };
        try
        {
            await foreach (var file in new ImageSearch(_browser).SearchAsync(CurrentFolder, options, cts.Token))
            {
                _files.Add(file);
                if (PassesTypeFilter(file))
                {
                    var item = new ThumbnailItemViewModel(file);
                    _itemsByPath[file.FullPath] = item;
                    Items.Add(item);
                    UpdateCounters();
                }
            }

            // Поиск закончен – сортируем и грузим миниатюры
            IsSearching = false;
            ShowFiles();
        }
        catch (OperationCanceledException)
        {
            // Поиск отменили (новый запрос или смена папки)
        }
    }

    // Вызывается из окна, когда меняется выделение в сетке
    public void SetSelection(List<ThumbnailItemViewModel> selected)
    {
        SelectedItems = selected;
        OnPropertyChanged(nameof(SelectedItem));
        _ = UpdatePreviewAsync();
    }

    // Загружает картинку для панели предпросмотра
    private async Task UpdatePreviewAsync()
    {
        int version = ++_previewVersion;
        var item = SelectedItem;
        if (item == null)
        {
            PreviewImage = null;
            PreviewInfo = "";
            return;
        }

        UpdatePreviewInfo();
        var file = item.File;
        var image = await Task.Run(() =>
        {
            using var bitmap = ThumbnailGenerator.LoadBitmap(file.FullPath, PreviewSize);
            return bitmap != null ? ImageHelper.FromSkBitmap(bitmap) : null;
        });

        // Пока грузили, могли выбрать другой файл
        if (version == _previewVersion)
            PreviewImage = image;
    }

    // Краткая информация под предпросмотром: разрешение, размер, дата
    private void UpdatePreviewInfo()
    {
        var file = SelectedItem?.File;
        if (file == null)
            return;
        var info = $"{Formatting.FormatSize(file.Size)}   {file.LastWriteTime:dd.MM.yyyy HH:mm}";
        PreviewInfo = file.Width.HasValue ? $"{file.Width} x {file.Height}   {info}" : info;
    }

    // Загружает миниатюры: все или только те, которых ещё нет
    private void LoadThumbnails(bool onlyMissing = false)
    {
        var toLoad = Items.Where(i => !onlyMissing || i.IsLoading).ToList();
        foreach (var item in toLoad)
            item.IsLoading = true;
        _loadTotal = toLoad.Count;
        LoadedCount = 0;
        _ = _loader.LoadAsync(toLoad.Select(i => i.File).ToList(), ThumbnailSize);
    }

    // Вызывается из фонового потока, когда готова очередная миниатюра
    private void Loader_ThumbnailReady(string path, byte[]? data)
    {
        var image = data != null ? ImageHelper.FromBytes(data) : null;
        Application.Current.Dispatcher.BeginInvoke(() =>
        {
            if (_itemsByPath.TryGetValue(path, out var item))
            {
                item.SetThumbnail(image);
                // Разрешение становится известно только после загрузки миниатюры
                if (item == SelectedItem)
                    UpdatePreviewInfo();
            }
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
