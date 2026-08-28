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

    // История переходов для кнопок «Назад» и «Вперёд»
    private readonly Stack<string> _backHistory = new();
    private readonly Stack<string> _forwardHistory = new();

    // Корни дерева папок
    public ObservableCollection<FolderItemViewModel> RootItems { get; } = [];

    // Файлы текущей папки
    public ObservableCollection<ImageFileInfo> Items { get; } = [];

    [ObservableProperty]
    public partial string CurrentFolder { get; set; } = "";

    // Текст в адресной строке (можно править вручную)
    [ObservableProperty]
    public partial string AddressText { get; set; } = "";

    public MainViewModel()
    {
        _browser = new FolderBrowser(App.Settings);
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
        Items.Clear();
        foreach (var file in ImageSorter.Sort(_browser.GetImages(CurrentFolder), SortField.Name))
            Items.Add(file);
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
